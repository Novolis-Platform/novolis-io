using Microsoft.Win32;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Novolis.IO.SystemMetrics;

internal sealed class WindowsSystemMetricsProvider : PlatformSystemMetricsProvider
{
    private readonly string _cpuName =
        Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
        ?? "Windows CPU";
    private long? _previousIdle;
    private long? _previousKernel;
    private long? _previousUser;
    private IReadOnlyList<GpuAdapterInfo> _gpus = Array.Empty<GpuAdapterInfo>();

    public override string Name => "Windows";

    protected override ValueTask<(
        CpuInfo Cpu,
        IReadOnlyList<GpuAdapterInfo> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformInventoryAsync(CancellationToken cancellationToken)
    {
        var diagnostics = new List<SystemMetricDiagnostic>();
        _gpus = OperatingSystem.IsWindows()
            ? ReadGpuInventory(diagnostics)
            : Array.Empty<GpuAdapterInfo>();
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
            .Select(adapter => new GpuMetrics(
                adapter.Id,
                null,
                new Dictionary<string, double>(StringComparer.Ordinal),
                null,
                adapter.DedicatedMemoryBytes,
                null,
                adapter.SharedMemoryBytes,
                null,
                null,
                SystemMetricStatus.Unavailable))
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
            if (!GetSystemTimes(
                    out var idle,
                    out var kernel,
                    out var user))
            {
                throw new InvalidOperationException(
                    "GetSystemTimes returned false.");
            }

            var idleTicks = idle.ToInt64();
            var kernelTicks = kernel.ToInt64();
            var userTicks = user.ToInt64();
            var utilization = CalculateUtilization(
                idleTicks,
                kernelTicks,
                userTicks);
            _previousIdle = idleTicks;
            _previousKernel = kernelTicks;
            _previousUser = userTicks;
            return new CpuMetrics(
                utilization,
                kernelTicks + userTicks,
                idleTicks);
        }
        catch (Exception ex) when (ex is DllNotFoundException
            or EntryPointNotFoundException
            or InvalidOperationException)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(WindowsSystemMetricsProvider),
                ex.Message,
                SystemMetricStatus.Unavailable));
            return new CpuMetrics(
                null,
                null,
                null,
                SystemMetricStatus.Unavailable);
        }
    }

    private double? CalculateUtilization(
        long idle,
        long kernel,
        long user)
    {
        if (_previousIdle is not { } previousIdle
            || _previousKernel is not { } previousKernel
            || _previousUser is not { } previousUser)
        {
            return null;
        }

        var idleDelta = idle - previousIdle;
        var totalDelta =
            kernel - previousKernel
            + user - previousUser;
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
            var status = new MemoryStatus
            {
                Length = (uint)Marshal.SizeOf<MemoryStatus>(),
            };
            if (!GlobalMemoryStatusEx(ref status))
                throw new InvalidOperationException(
                    "GlobalMemoryStatusEx returned false.");

            var used = status.TotalPhysical - status.AvailablePhysical;
            return new MemoryMetrics(
                ToNullableLong(status.TotalPhysical),
                ToNullableLong(status.AvailablePhysical),
                ToNullableLong(used));
        }
        catch (Exception ex) when (ex is DllNotFoundException
            or EntryPointNotFoundException
            or InvalidOperationException)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(WindowsSystemMetricsProvider),
                ex.Message,
                SystemMetricStatus.Unavailable));
            return new MemoryMetrics(
                null,
                null,
                null,
                SystemMetricStatus.Unavailable);
        }
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<GpuAdapterInfo> ReadGpuInventory(
        List<SystemMetricDiagnostic> diagnostics)
    {
        var adapters = new List<GpuAdapterInfo>();
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4D36E968-E325-11CE-BFC1-08002BE10318}");
            if (classKey is null)
            {
                diagnostics.Add(new SystemMetricDiagnostic(
                    nameof(WindowsSystemMetricsProvider),
                    "The Windows display adapter registry key was not found.",
                    SystemMetricStatus.Unavailable));
                return adapters;
            }

            foreach (var name in classKey.GetSubKeyNames())
            {
                if (!int.TryParse(name, out _))
                    continue;
                using var adapterKey = classKey.OpenSubKey(name);
                if (adapterKey is null)
                    continue;

                var adapterName =
                    adapterKey.GetValue("DriverDesc") as string
                    ?? adapterKey.GetValue("HardwareInformation.AdapterString") as string;
                if (string.IsNullOrWhiteSpace(adapterName))
                    continue;

                var deviceId = adapterKey.GetValue("MatchingDeviceId") as string;
                var id = adapterKey.GetValue("DeviceInstanceID") as string
                    ?? deviceId
                    ?? $"windows-registry-{name}";
                adapters.Add(new GpuAdapterInfo(
                    id,
                    adapterName,
                    ReadVendor(deviceId),
                    adapterKey.GetValue("DriverVersion") as string,
                    "WDDM",
                    ReadRegistryLong(
                        adapterKey.GetValue("HardwareInformation.MemorySize")),
                    null));
            }
        }
        catch (Exception ex) when (ex is SecurityException
            or UnauthorizedAccessException
            or IOException)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(WindowsSystemMetricsProvider),
                ex.Message,
                SystemMetricStatus.PermissionDenied));
        }

        return adapters;
    }

    private static IReadOnlyList<ScreenInfo> ReadScreens(
        List<SystemMetricDiagnostic> diagnostics)
    {
        var screens = new List<ScreenInfo>();
        try
        {
            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (monitor, _, _, _) =>
                {
                    var info = new MonitorInfo
                    {
                        Size = Marshal.SizeOf<MonitorInfo>(),
                    };
                    if (!GetMonitorInfo(monitor, ref info))
                        return true;

                    var width = info.Monitor.Right - info.Monitor.Left;
                    var height = info.Monitor.Bottom - info.Monitor.Top;
                    screens.Add(new ScreenInfo(
                        info.DeviceName,
                        info.DeviceName,
                        info.Monitor.Left,
                        info.Monitor.Top,
                        width,
                        height,
                        ReadDpi(monitor),
                        null,
                        (info.Flags & 1) != 0,
                        null));
                    return true;
                },
                IntPtr.Zero);
        }
        catch (Exception ex) when (ex is DllNotFoundException
            or EntryPointNotFoundException
            or InvalidOperationException)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(WindowsSystemMetricsProvider),
                ex.Message,
                SystemMetricStatus.Unavailable));
        }

        return screens;
    }

    private static uint? ReadDpi(IntPtr monitor)
    {
        try
        {
            var result = GetDpiForMonitor(
                monitor,
                DpiType.Effective,
                out var dpiX,
                out _);
            return result == 0 ? dpiX : 96;
        }
        catch (DllNotFoundException)
        {
            return 96;
        }
        catch (EntryPointNotFoundException)
        {
            return 96;
        }
    }

    private static string? ReadVendor(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return null;
        var marker = "VEN_";
        var index = deviceId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 || index + marker.Length + 4 > deviceId.Length
            ? null
            : deviceId.Substring(index + marker.Length, 4);
    }

    private static long? ReadRegistryLong(object? value) =>
        value switch
        {
            long number => number,
            int number => number,
            byte[] bytes when bytes.Length >= sizeof(long) =>
                BitConverter.ToInt64(bytes, 0),
            _ => null,
        };

    private static long? ToNullableLong(ulong value) =>
        value <= long.MaxValue ? (long)value : null;

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idleTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(
        ref MemoryStatus buffer);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clip,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitor,
        ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        DpiType dpiType,
        out uint dpiX,
        out uint dpiY);

    private delegate bool MonitorEnumProc(
        IntPtr monitor,
        IntPtr deviceContext,
        IntPtr rectangle,
        IntPtr data);

    private enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;

        public readonly long ToInt64() =>
            ((long)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
