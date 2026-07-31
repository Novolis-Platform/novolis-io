using System.Globalization;
using System.Text;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;

namespace Novolis.IO.Mobile.Android;

/// <summary>
/// Slim host-side Android Debug Bridge client.
/// Uses the ADB wire protocol via AdvancedSharpAdbClient (not per-call CLI parsing).
/// The platform-tools <c>adb</c> binary is only required to host/ensure the local adb server.
/// </summary>
public sealed class AndroidDebugBridge
{
    private readonly AdbClient _client;
    private readonly IAdbProcessRunner? _cli;

    /// <summary>
    /// Creates a client, resolving <c>adb</c>, ensuring the server is running, then connecting over the protocol.
    /// </summary>
    /// <param name="adbPath">Optional explicit path to <c>adb</c> / <c>adb.exe</c>.</param>
    public AndroidDebugBridge(string? adbPath = null)
    {
        AdbPath = AdbLocator.Resolve(adbPath);
        EnsureServer(AdbPath);
        _client = new AdbClient();
        Transport = "protocol";
    }

    /// <summary>
    /// Creates a client that still uses the protocol for device work, but keeps a CLI runner for <see cref="Run"/>.
    /// </summary>
    public AndroidDebugBridge(IAdbProcessRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _cli = runner;
        AdbPath = AdbLocator.Resolve(runner.AdbPath);
        EnsureServer(AdbPath);
        _client = new AdbClient();
        Transport = "protocol";
    }

    /// <summary>Path to the <c>adb</c> binary used to host the server.</summary>
    public string AdbPath { get; }

    /// <summary>Transport in use (<c>protocol</c> for AdvancedSharpAdbClient).</summary>
    public string Transport { get; }

    /// <summary>Lists devices via the ADB protocol (<c>host:devices-l</c>).</summary>
    public IReadOnlyList<AdbDevice> ListDevices()
    {
        return _client.GetDevices()
            .Select(MapDevice)
            .ToList();
    }

    /// <summary>Returns the connection state for <paramref name="serial"/> (or the default online device).</summary>
    public string GetState(string? serial = null)
    {
        var device = RequireDevice(serial);
        return MapState(device.State).ToString().ToLowerInvariant() switch
        {
            "device" => "device",
            var s => s,
        };
    }

    /// <summary>Reads a single <c>getprop</c> value over a protocol shell session.</summary>
    public string? GetProp(string propertyName, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        var result = Shell($"getprop {propertyName}", serial);
        if (!result.Ok)
            throw new InvalidOperationException($"getprop failed: {result.Diagnostic}");
        var value = result.StdOut.Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>Reads a getprop value without throwing when empty or failing.</summary>
    public string? TryGetProp(string propertyName, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        var result = Shell($"getprop {propertyName}", serial);
        if (!result.Ok)
            return null;
        var value = result.StdOut.Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Collects identity props plus battery, memory, display, storage, CPU, and build technical fields
    /// via protocol shell commands.
    /// </summary>
    public AndroidDeviceInfo GetDeviceInfo(string? serial = null)
    {
        var device = RequireDevice(serial);
        var resolved = device.Serial;

        string? Prop(string name) => TryGetProp(name, resolved);

        var batteryRaw = SoftShell(resolved, "dumpsys battery");
        var memRaw = SoftShell(resolved, "grep -E 'MemTotal|MemAvailable|MemFree|SwapTotal|SwapFree' /proc/meminfo");
        var sizeRaw = SoftShell(resolved, "wm size");
        var densRaw = SoftShell(resolved, "wm density");
        var dfRaw = SoftShell(resolved, "df -h /data /system /sdcard /storage/emulated 2>/dev/null");
        var upRaw = SoftShell(resolved, "cat /proc/uptime");
        var cpuCountRaw = SoftShell(resolved, "grep -c ^processor /proc/cpuinfo");
        var cpuHwRaw = SoftShell(resolved, "grep -m1 -E 'Hardware|model name|Processor' /proc/cpuinfo");
        var androidIdRaw = SoftShell(resolved, "settings get secure android_id");
        var displayExtra = SoftShell(resolved, "dumpsys display | head -n 40");

        var extras = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(displayExtra))
        {
            extras.AppendLine("dumpsys display (head):");
            extras.AppendLine(displayExtra.TrimEnd());
        }

        return new AndroidDeviceInfo
        {
            Serial = resolved,
            State = MapState(device.State) == AdbDeviceState.Device ? "device" : MapState(device.State).ToString().ToLowerInvariant(),
            Model = Prop("ro.product.model") ?? NullIfEmpty(device.Model),
            Manufacturer = Prop("ro.product.manufacturer"),
            Brand = Prop("ro.product.brand"),
            ProductName = Prop("ro.product.name") ?? NullIfEmpty(device.Product),
            Device = Prop("ro.product.device") ?? NullIfEmpty(device.Name),
            Board = Prop("ro.product.board"),
            Hardware = Prop("ro.hardware"),
            AndroidVersion = Prop("ro.build.version.release"),
            SdkVersion = Prop("ro.build.version.sdk"),
            FirstApiLevel = Prop("ro.product.first_api_level"),
            SecurityPatch = Prop("ro.build.version.security_patch"),
            BuildDisplay = Prop("ro.build.display.id"),
            BuildId = Prop("ro.build.id"),
            BuildType = Prop("ro.build.type"),
            BuildTags = Prop("ro.build.tags"),
            Fingerprint = Prop("ro.build.fingerprint"),
            Bootloader = Prop("ro.bootloader"),
            Baseband = Prop("gsm.version.baseband"),
            HardwareSerial = Prop("ro.serialno"),
            AndroidId = NullIfLiteral(androidIdRaw?.Trim(), "null"),
            Abi = Prop("ro.product.cpu.abi"),
            AbiList = Prop("ro.product.cpu.abilist"),
            CpuCoreCount = int.TryParse(cpuCountRaw?.Trim(), out var cores) ? cores : null,
            CpuHardware = NullIfLiteral(Prop("ro.soc.model"), null)
                ?? NullIfLiteral(Prop("ro.board.platform"), null)
                ?? ExtractCpuHardware(cpuHwRaw),
            Timezone = Prop("persist.sys.timezone"),
            UptimeSeconds = ParseUptimeSeconds(upRaw),
            Battery = ParseBattery(batteryRaw),
            Memory = ParseMemory(memRaw),
            Display = ParseDisplay(sizeRaw, densRaw),
            Storage = ParseDf(dfRaw),
            RawExtras = extras.Length == 0 ? null : extras.ToString(),
        };
    }

    /// <summary>Installs an APK via the ADB protocol install service.</summary>
    public AdbOperationResult Install(string apkPath, bool reinstall = true, string? serial = null)
    {
        var args = reinstall ? new[] { "-r" } : Array.Empty<string>();
        return Install(apkPath, serial, args);
    }

    /// <summary>Installs an APK with explicit <c>adb install</c> flags (e.g. <c>-r</c>, <c>-g</c>, <c>-d</c>).</summary>
    public AdbOperationResult Install(string apkPath, string? serial, params string[] installArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apkPath);
        ArgumentNullException.ThrowIfNull(installArguments);
        var full = Path.GetFullPath(apkPath);
        if (!File.Exists(full))
            return AdbOperationResult.Fail("install", $"APK not found: {full}");

        try
        {
            var device = RequireDevice(serial);
            using var stream = File.OpenRead(full);
            _client.Install(device, stream, callback: null, installArguments);
            return AdbOperationResult.Success("install", $"Installed {Path.GetFileName(full)}.");
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail("install", ex.Message);
        }
    }

    /// <summary>
    /// Polls until a device is online (<see cref="AdbDeviceState.Device"/>) or <paramref name="timeout"/> elapses.
    /// </summary>
    public AdbDevice WaitForDevice(TimeSpan timeout, string? serial = null, TimeSpan? pollInterval = null)
    {
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var poll = pollInterval is { } p && p > TimeSpan.Zero ? p : TimeSpan.FromMilliseconds(250);
        var deadline = DateTime.UtcNow + timeout;
        InvalidOperationException? last = null;

        while (true)
        {
            try
            {
                var devices = ListDevices();
                var resolved = ResolveSerial(serial);
                AdbDevice? match;
                if (resolved is null)
                {
                    match = devices.FirstOrDefault(d => d.State == AdbDeviceState.Device)
                            ?? devices.FirstOrDefault();
                }
                else
                {
                    match = devices.FirstOrDefault(d =>
                        string.Equals(d.Serial, resolved, StringComparison.OrdinalIgnoreCase));
                }

                if (match is { State: AdbDeviceState.Device })
                    return match;

                if (match is not null)
                    last = new InvalidOperationException(
                        $"Device {match.Serial} is {match.State}; waiting for Device/online.");
                else if (resolved is not null)
                    last = new InvalidOperationException($"Device '{resolved}' not found.");
                else
                    last = new InvalidOperationException("No adb devices connected.");
            }
            catch (Exception ex)
            {
                last = new InvalidOperationException(ex.Message, ex);
            }

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"Timed out after {timeout} waiting for a ready adb device." +
                    (last is null ? "" : $" Last: {last.Message}"));

            Thread.Sleep(poll);
        }
    }

    /// <summary>Queries whether a package is installed and reads version fields when available.</summary>
    public AndroidPackageInfo? TryGetPackageInfo(string packageName, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        var path = Shell($"pm path {packageName}", serial);
        var dump = SoftShell(serial,
            $"dumpsys package {packageName} | grep -E 'versionName=|versionCode=' | head -n 8");
        return AndroidAppInstaller.ParsePackageInfo(
            packageName,
            path.Ok ? path.StdOut : null,
            dump);
    }

    /// <summary>Starts an app via <c>monkey</c> launcher intent (package main activity).</summary>
    public AdbOperationResult StartApp(string packageName, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        // monkey -p <pkg> -c LAUNCHER 1 is widely available without resolving the activity class.
        var result = Shell(
            $"monkey -p {packageName} -c android.intent.category.LAUNCHER 1",
            serial);
        if (!result.Ok)
            return AdbOperationResult.Fail("startapp", result.Diagnostic, result);
        return AdbOperationResult.Success("startapp", $"Started {packageName}.", result);
    }

    /// <summary>Force-stops a package.</summary>
    public AdbOperationResult ForceStop(string packageName, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        var result = Shell($"am force-stop {packageName}", serial);
        if (!result.Ok)
            return AdbOperationResult.Fail("forcestop", result.Diagnostic, result);
        return AdbOperationResult.Success("forcestop", $"Force-stopped {packageName}.", result);
    }

    /// <summary>Uninstalls a package via the ADB protocol.</summary>
    public AdbOperationResult Uninstall(string packageName, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        try
        {
            var device = RequireDevice(serial);
            _client.Uninstall(device, packageName);
            return AdbOperationResult.Success("uninstall", $"Uninstalled {packageName}.");
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail("uninstall", ex.Message);
        }
    }

    /// <summary>Pushes a local file via the ADB sync protocol.</summary>
    public AdbOperationResult Push(string localPath, string remotePath, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        var full = Path.GetFullPath(localPath);
        if (!File.Exists(full))
            return AdbOperationResult.Fail("push", $"Local file not found: {full}");

        try
        {
            var device = RequireDevice(serial);
            using var sync = new SyncService(_client, device);
            using var stream = File.OpenRead(full);
            sync.Push(stream, remotePath, UnixFileStatus.DefaultFileMode, DateTimeOffset.Now, null);
            return AdbOperationResult.Success("push", $"Pushed → {remotePath}");
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail("push", ex.Message);
        }
    }

    /// <summary>Pulls a remote path via the ADB sync protocol.</summary>
    public AdbOperationResult Pull(string remotePath, string localPath, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        var full = Path.GetFullPath(localPath);

        try
        {
            var device = RequireDevice(serial);
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using var sync = new SyncService(_client, device);
            using var stream = File.Create(full);
            sync.Pull(remotePath, stream, null);
            return AdbOperationResult.Success("pull", $"Pulled → {full}");
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail("pull", ex.Message);
        }
    }

    /// <summary>Runs a remote shell command over the ADB protocol.</summary>
    public AdbProcessResult Shell(string command, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        try
        {
            var device = RequireDevice(serial);
            var receiver = new ConsoleOutputReceiver();
            _client.ExecuteRemoteCommand(command, device, receiver, Encoding.UTF8);
            return new AdbProcessResult(0, receiver.ToString() ?? "", "");
        }
        catch (Exception ex)
        {
            return new AdbProcessResult(1, "", ex.Message);
        }
    }

    /// <summary>
    /// Escape hatch: runs raw <c>adb</c> CLI args (requires a <see cref="IAdbProcessRunner"/>).
    /// Prefer protocol methods for normal use.
    /// </summary>
    public AdbProcessResult Run(string? serial, params string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var runner = _cli ?? new ProcessAdbRunner(AdbPath);
        var list = new List<string>(args.Length + 2);
        var resolved = ResolveSerial(serial);
        if (resolved is not null)
        {
            list.Add("-s");
            list.Add(resolved);
        }

        list.AddRange(args);
        return runner.Run(list.ToArray());
    }

    private string? SoftShell(string? serial, string command)
    {
        var result = Shell(command, serial);
        return string.IsNullOrWhiteSpace(result.StdOut) ? null : result.StdOut;
    }

    private DeviceData RequireDevice(string? serial)
    {
        var devices = _client.GetDevices().ToList();
        if (devices.Count == 0)
            throw new InvalidOperationException("No adb devices connected.");

        var resolved = ResolveSerial(serial);
        DeviceData? device;
        if (resolved is null)
        {
            device = devices.FirstOrDefault(d => d.State == DeviceState.Online)
                     ?? devices[0];
        }
        else
        {
            device = devices.FirstOrDefault(d =>
                string.Equals(d.Serial, resolved, StringComparison.OrdinalIgnoreCase));
            if (device is null)
                throw new InvalidOperationException($"Device '{resolved}' not found.");
        }

        return device;
    }

    private static string? ResolveSerial(string? serial)
    {
        if (!string.IsNullOrWhiteSpace(serial))
            return serial.Trim();
        var env = Environment.GetEnvironmentVariable("ANDROID_SERIAL");
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    private static void EnsureServer(string adbPath)
    {
        if (!File.Exists(adbPath))
            throw new FileNotFoundException($"adb executable not found: {adbPath}", adbPath);

        var server = AdbServer.Instance;
        AdbServerStatus status;
        try
        {
            status = server.GetStatus();
        }
        catch
        {
            status = default;
        }

        if (status.IsRunning)
            return;

        var result = server.StartServer(adbPath, restartServerIfNewer: false);
        if (result is StartServerResult.Started
            or StartServerResult.AlreadyRunning
            or StartServerResult.RestartedOutdatedDaemon
            or StartServerResult.Starting)
        {
            // Starting may need a brief settle.
            for (var i = 0; i < 20; i++)
            {
                try
                {
                    if (server.GetStatus().IsRunning)
                        return;
                }
                catch
                {
                    // retry
                }

                Thread.Sleep(50);
            }
        }

        throw new InvalidOperationException(
            $"Failed to start adb server from '{adbPath}' (result={result}).");
    }

    private static AdbDevice MapDevice(DeviceData d) =>
        new(
            d.Serial ?? "",
            MapState(d.State),
            NullIfEmpty(d.Product),
            NullIfEmpty(d.Model),
            NullIfEmpty(d.Name),
            NullIfEmpty(d.TransportId));

    private static AdbDeviceState MapState(DeviceState state) =>
        state switch
        {
            DeviceState.Online => AdbDeviceState.Device,
            DeviceState.Unauthorized => AdbDeviceState.Unauthorized,
            DeviceState.Offline => AdbDeviceState.Offline,
            DeviceState.NoPermissions => AdbDeviceState.NoPermissions,
            DeviceState.BootLoader => AdbDeviceState.Bootloader,
            DeviceState.Recovery => AdbDeviceState.Recovery,
            DeviceState.Sideload => AdbDeviceState.Sideload,
            _ => AdbDeviceState.Unknown,
        };

    /// <summary>Parses classic <c>adb devices -l</c> stdout (unit tests / CLI fallback).</summary>
    public static IReadOnlyList<AdbDevice> ParseDevices(string stdout)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        var list = new List<AdbDevice>();
        using var reader = new StringReader(stdout);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            var serial = parts[0];
            var stateToken = parts[1];
            var tagStart = 2;
            if (parts.Length >= 3
                && stateToken.Equals("no", StringComparison.OrdinalIgnoreCase)
                && parts[2].Equals("permissions", StringComparison.OrdinalIgnoreCase))
            {
                stateToken = "no permissions";
                tagStart = 3;
            }

            var state = ParseState(stateToken);
            string? product = null, model = null, device = null, transportId = null;
            for (var i = tagStart; i < parts.Length; i++)
            {
                var tag = parts[i];
                var colon = tag.IndexOf(':');
                if (colon <= 0 || colon == tag.Length - 1)
                    continue;
                var key = tag[..colon];
                var value = tag[(colon + 1)..];
                switch (key)
                {
                    case "product":
                        product = value;
                        break;
                    case "model":
                        model = value;
                        break;
                    case "device":
                        device = value;
                        break;
                    case "transport_id":
                        transportId = value;
                        break;
                }
            }

            list.Add(new AdbDevice(serial, state, product, model, device, transportId));
        }

        return list;
    }

    /// <summary>Maps an <c>adb devices</c> state token to <see cref="AdbDeviceState"/>.</summary>
    public static AdbDeviceState ParseState(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "device" => AdbDeviceState.Device,
            "unauthorized" => AdbDeviceState.Unauthorized,
            "offline" => AdbDeviceState.Offline,
            "no" or "no permissions" => AdbDeviceState.NoPermissions,
            "bootloader" => AdbDeviceState.Bootloader,
            "recovery" => AdbDeviceState.Recovery,
            "sideload" => AdbDeviceState.Sideload,
            _ => AdbDeviceState.Unknown,
        };

    /// <summary>Parses <c>dumpsys battery</c> text.</summary>
    public static AndroidBatteryInfo ParseBattery(string? raw)
    {
        var map = ParseKeyValues(raw);
        return new AndroidBatteryInfo
        {
            Level = GetInt(map, "level"),
            Scale = GetInt(map, "scale"),
            Status = GetInt(map, "status"),
            Health = GetInt(map, "health"),
            VoltageMv = GetInt(map, "voltage"),
            TemperatureTenthsC = GetInt(map, "temperature"),
            Technology = GetString(map, "technology"),
            AcPowered = GetBool(map, "AC powered"),
            UsbPowered = GetBool(map, "USB powered"),
            WirelessPowered = GetBool(map, "Wireless powered"),
            Present = GetBool(map, "present"),
        };
    }

    /// <summary>Parses selected <c>/proc/meminfo</c> lines.</summary>
    public static AndroidMemoryInfo ParseMemory(string? raw)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                var key = line[..colon].Trim();
                var rest = line[(colon + 1)..].Trim();
                var tok = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length > 0 && long.TryParse(tok[0], out var kb))
                    map[key] = kb;
            }
        }

        return new AndroidMemoryInfo
        {
            MemTotalKb = map.TryGetValue("MemTotal", out var total) ? total : null,
            MemAvailableKb = map.TryGetValue("MemAvailable", out var avail) ? avail : null,
            MemFreeKb = map.TryGetValue("MemFree", out var free) ? free : null,
            SwapTotalKb = map.TryGetValue("SwapTotal", out var swapT) ? swapT : null,
            SwapFreeKb = map.TryGetValue("SwapFree", out var swapF) ? swapF : null,
        };
    }

    /// <summary>Parses <c>wm size</c> / <c>wm density</c> output.</summary>
    public static AndroidDisplayInfo ParseDisplay(string? sizeRaw, string? densityRaw)
    {
        string? physical = null;
        int? w = null, h = null;
        if (!string.IsNullOrWhiteSpace(sizeRaw))
        {
            foreach (var line in sizeRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith("Physical size:", StringComparison.OrdinalIgnoreCase))
                {
                    physical = line["Physical size:".Length..].Trim();
                    var dims = physical.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (dims.Length == 2
                        && int.TryParse(dims[0], out var pw)
                        && int.TryParse(dims[1], out var ph))
                    {
                        w = pw;
                        h = ph;
                    }
                }
            }
        }

        int? dpi = null;
        string? overrideDens = null;
        if (!string.IsNullOrWhiteSpace(densityRaw))
        {
            foreach (var line in densityRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith("Physical density:", StringComparison.OrdinalIgnoreCase))
                {
                    var tok = line["Physical density:".Length..].Trim();
                    if (int.TryParse(tok, out var d))
                        dpi = d;
                }
                else if (line.StartsWith("Override density:", StringComparison.OrdinalIgnoreCase))
                {
                    overrideDens = line["Override density:".Length..].Trim();
                }
            }
        }

        return new AndroidDisplayInfo
        {
            PhysicalSize = physical,
            WidthPx = w,
            HeightPx = h,
            DensityDpi = dpi,
            OverrideDensity = overrideDens,
        };
    }

    /// <summary>Parses <c>df -h</c> rows.</summary>
    public static IReadOnlyList<AndroidStorageMount> ParseDf(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var list = new List<AndroidStorageMount>();
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("Filesystem", StringComparison.OrdinalIgnoreCase))
                continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                continue;
            var mounted = string.Join(' ', parts.Skip(5));
            list.Add(new AndroidStorageMount(parts[0], parts[1], parts[2], parts[3], parts[4], mounted));
        }

        return list;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? NullIfLiteral(string? value, string? literal)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (literal is not null && string.Equals(value, literal, StringComparison.OrdinalIgnoreCase))
            return null;
        return value;
    }

    private static string? ExtractCpuHardware(string? cpuinfoLine)
    {
        if (string.IsNullOrWhiteSpace(cpuinfoLine))
            return null;
        var colon = cpuinfoLine.IndexOf(':');
        var value = (colon < 0 ? cpuinfoLine : cpuinfoLine[(colon + 1)..]).Trim();
        if (value.Length == 0 || value.Any(c => char.IsControl(c) || c == '\uFFFD'))
            return null;
        return value;
    }

    private static double? ParseUptimeSeconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var first = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return first.Length > 0 && double.TryParse(first[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)
            ? sec
            : null;
    }

    private static Dictionary<string, string> ParseKeyValues(string? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return map;

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;
            map[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return map;
    }

    private static int? GetInt(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : null;

    private static bool? GetBool(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : null;

    private static string? GetString(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var v) && v.Length > 0 ? v : null;
}
