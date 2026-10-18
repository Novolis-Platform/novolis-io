namespace Novolis.IO.SystemMetrics;

internal sealed class LinuxSystemMetricsProvider : PlatformSystemMetricsProvider
{
    private readonly string _cpuName = ReadCpuName();
    private long? _previousTotal;
    private long? _previousIdle;
    private IReadOnlyList<GpuAdapterInfo> _gpus = Array.Empty<GpuAdapterInfo>();

    public override string Name => "Linux";

    protected override ValueTask<(
        CpuInfo Cpu,
        IReadOnlyList<GpuAdapterInfo> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformInventoryAsync(CancellationToken cancellationToken)
    {
        var diagnostics = new List<SystemMetricDiagnostic>();
        _gpus = ReadGpuInventory(diagnostics);
        var screens = ReadScreens(diagnostics);
        return ValueTask.FromResult((
            new CpuInfo(
                _cpuName,
                Environment.ProcessorCount,
                null,
                null),
            _gpus,
            screens,
            (IReadOnlyList<SystemMetricDiagnostic>)diagnostics));
    }

    protected override ValueTask<(
        CpuMetrics Cpu,
        MemoryMetrics Memory,
        IReadOnlyList<GpuMetrics> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformSampleAsync(
            SystemMetricsSamplingContext context,
            CancellationToken cancellationToken)
    {
        var diagnostics = new List<SystemMetricDiagnostic>();
        var cpu = ReadCpu(diagnostics);
        var memory = ReadMemory(diagnostics);
        var gpus = _gpus
            .Select(adapter => ReadGpu(adapter, diagnostics))
            .ToArray();
        var screens = ReadScreens(diagnostics);
        return ValueTask.FromResult((
            cpu,
            memory,
            (IReadOnlyList<GpuMetrics>)gpus,
            screens,
            (IReadOnlyList<SystemMetricDiagnostic>)diagnostics));
    }

    private CpuMetrics ReadCpu(List<SystemMetricDiagnostic> diagnostics)
    {
        try
        {
            var line = File.ReadLines("/proc/stat")
                .FirstOrDefault(value => value.StartsWith("cpu ", StringComparison.Ordinal));
            if (line is null)
                throw new IOException("The Linux CPU statistics file has no aggregate row.");

            var values = line
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(long.Parse)
                .ToArray();
            if (values.Length < 4)
                throw new IOException("The Linux CPU statistics row is incomplete.");

            var idle = values[3] + (values.Length > 4 ? values[4] : 0);
            var total = values.Take(Math.Min(values.Length, 8)).Sum();
            var utilization = CalculateUtilization(total, idle);
            _previousTotal = total;
            _previousIdle = idle;
            return new CpuMetrics(utilization, total, idle);
        }
        catch (Exception)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(LinuxSystemMetricsProvider),
                "Unable to read /proc/stat.",
                SystemMetricStatus.Unavailable));
            return new CpuMetrics(
                null,
                null,
                null,
                SystemMetricStatus.Unavailable);
        }
    }

    private double? CalculateUtilization(long total, long idle)
    {
        if (_previousTotal is not { } previousTotal
            || _previousIdle is not { } previousIdle)
        {
            return null;
        }

        var totalDelta = total - previousTotal;
        var idleDelta = idle - previousIdle;
        if (totalDelta <= 0 || idleDelta < 0 || idleDelta > totalDelta)
            return null;
        return Math.Clamp(
            (totalDelta - idleDelta) / (double)totalDelta * 100,
            0,
            100);
    }

    private static MemoryMetrics ReadMemory(
        List<SystemMetricDiagnostic> diagnostics)
    {
        try
        {
            var values = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (ParseMemoryLine(line) is { } parsed)
                    values[parsed.Key] = parsed.Bytes;
            }
            if (!values.TryGetValue("MemTotal", out var total))
                throw new IOException("MemTotal is not present in /proc/meminfo.");
            values.TryGetValue("MemAvailable", out var available);
            long? used = available is { } availableBytes
                ? total - availableBytes
                : null;
            return new MemoryMetrics(total, available, used);
        }
        catch (Exception)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(LinuxSystemMetricsProvider),
                "Unable to read /proc/meminfo.",
                SystemMetricStatus.Unavailable));
            return new MemoryMetrics(
                null,
                null,
                null,
                SystemMetricStatus.Unavailable);
        }
    }

    private static GpuMetrics ReadGpu(
        GpuAdapterInfo adapter,
        List<SystemMetricDiagnostic> diagnostics)
    {
        var card = adapter.Id["linux-drm-".Length..];
        var devicePath = $"/sys/class/drm/{card}/device";
        var busy = ReadLong($"{devicePath}/gpu_busy_percent");
        var dedicatedUsed = ReadLong($"{devicePath}/mem_info_vram_used");
        var sharedUsed = ReadLong($"{devicePath}/mem_info_gtt_used");
        var status = busy is not null
            || dedicatedUsed is not null
            || sharedUsed is not null
            ? SystemMetricStatus.Supported
            : SystemMetricStatus.Unavailable;
        if (status != SystemMetricStatus.Supported)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(LinuxSystemMetricsProvider),
                $"No GPU utilization counters were found for {adapter.Name}.",
                status,
                adapter.Id));
        }

        return new GpuMetrics(
            adapter.Id,
            busy,
            new Dictionary<string, double>(StringComparer.Ordinal),
            dedicatedUsed,
            adapter.DedicatedMemoryBytes,
            sharedUsed,
            adapter.SharedMemoryBytes,
            null,
            null,
            status);
    }

    private static IReadOnlyList<GpuAdapterInfo> ReadGpuInventory(
        List<SystemMetricDiagnostic> diagnostics)
    {
        var adapters = new List<GpuAdapterInfo>();
        if (!Directory.Exists("/sys/class/drm"))
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(LinuxSystemMetricsProvider),
                "The DRM sysfs directory is not available.",
                SystemMetricStatus.Unsupported));
            return adapters;
        }

        foreach (var directory in Directory.EnumerateDirectories("/sys/class/drm", "card*"))
        {
            var name = Path.GetFileName(directory);
            if (name.Length <= 4
                || !int.TryParse(name["card".Length..], out _))
            {
                continue;
            }

            var devicePath = Path.Combine(directory, "device");
            var vendorId = ReadText(Path.Combine(devicePath, "vendor"));
            var deviceId = ReadText(Path.Combine(devicePath, "device"));
            var vendor = VendorName(vendorId);
            var adapterName = vendor is null
                ? $"Linux {name}"
                : $"{vendor} {name}";
            adapters.Add(new GpuAdapterInfo(
                $"linux-drm-{name}",
                adapterName,
                vendor,
                null,
                ReadDriver(devicePath),
                ReadLong(Path.Combine(devicePath, "mem_info_vram_total")),
                ReadLong(Path.Combine(devicePath, "mem_info_gtt_total"))));
        }

        if (adapters.Count == 0)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(LinuxSystemMetricsProvider),
                "No DRM graphics adapters were found.",
                SystemMetricStatus.Unavailable));
        }

        return adapters;
    }

    private static IReadOnlyList<ScreenInfo> ReadScreens(
        List<SystemMetricDiagnostic> diagnostics)
    {
        var screens = new List<ScreenInfo>();
        if (!Directory.Exists("/sys/class/drm"))
            return screens;

        foreach (var connector in Directory.EnumerateDirectories("/sys/class/drm", "card*-*"))
        {
            var status = ReadText(Path.Combine(connector, "status"));
            if (!string.Equals(status, "connected", StringComparison.OrdinalIgnoreCase))
                continue;

            var mode = ReadText(Path.Combine(connector, "modes"));
            var (width, height) = ParseMode(mode);
            var name = Path.GetFileName(connector);
            screens.Add(new ScreenInfo(
                name,
                name,
                0,
                0,
                width,
                height,
                null,
                null,
                false,
                name.StartsWith("card", StringComparison.Ordinal)
                    && name.IndexOf('-', StringComparison.Ordinal) > 0
                    ? $"linux-drm-{name[..name.IndexOf('-', StringComparison.Ordinal)]}"
                    : null));
        }

        if (screens.Count == 0)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(LinuxSystemMetricsProvider),
                "No connected DRM display connectors were found.",
                SystemMetricStatus.Unavailable));
        }

        return screens;
    }

    private static (int Width, int Height) ParseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return (0, 0);
        var first = mode
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (first is null)
            return (0, 0);
        var separator = first.IndexOf('x');
        return separator <= 0
            || !int.TryParse(first[..separator], out var width)
            || !int.TryParse(first[(separator + 1)..], out var height)
            ? (0, 0)
            : (width, height);
    }

    private static (string Key, long Bytes)? ParseMemoryLine(string line)
    {
        var parts = line.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !long.TryParse(parts[1], out var value))
            return null;
        var bytes = parts.Length > 2
            && string.Equals(parts[2], "kB", StringComparison.OrdinalIgnoreCase)
            ? value * 1024
            : value;
        return (parts[0].TrimEnd(':'), bytes);
    }

    private static string ReadCpuName()
    {
        try
        {
            var line = File.ReadLines("/proc/cpuinfo")
                .FirstOrDefault(value =>
                    value.StartsWith("model name", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("Hardware", StringComparison.OrdinalIgnoreCase));
            return line?.Split(':', 2).Last().Trim()
                ?? "Linux CPU";
        }
        catch (IOException)
        {
            return "Linux CPU";
        }
    }

    private static string? ReadDriver(string devicePath)
    {
        var uevent = ReadText(Path.Combine(devicePath, "uevent"));
        return uevent?
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2
                && string.Equals(parts[0], "DRIVER", StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[1])
            .FirstOrDefault();
    }

    private static string? VendorName(string? vendorId) =>
        vendorId?.ToLowerInvariant() switch
        {
            "0x10de" => "NVIDIA",
            "0x1002" => "AMD",
            "0x8086" => "Intel",
            _ => vendorId,
        };

    private static long? ReadLong(string path)
    {
        var value = ReadText(path);
        return long.TryParse(value, out var number) ? number : null;
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
