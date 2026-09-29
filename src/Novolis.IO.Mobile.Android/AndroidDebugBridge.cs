using System.Globalization;
using System.Text;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;

namespace Novolis.IO.Mobile.Android;

/// <summary>
/// Slim host-side Android Debug Bridge client.
/// Uses the ADB wire protocol via AdvancedSharpAdbClient (not per-call CLI parsing).
/// The platform-tools <c>adb</c> binary is only required to host/ensure the local adb server.
/// </summary>
public sealed class AndroidDebugBridge
{
    private readonly IAndroidAdbBackend _backend;
    private readonly IAdbProcessRunner? _cli;

    /// <summary>
    /// Creates a client, resolving <c>adb</c>, ensuring the server is running, then connecting over the protocol.
    /// </summary>
    /// <param name="adbPath">Optional explicit path to <c>adb</c> / <c>adb.exe</c>.</param>
    public AndroidDebugBridge(string? adbPath = null)
    {
        AdbPath = AdbLocator.Resolve(adbPath);
        EnsureServer(AdbPath);
        _backend = new ProtocolAndroidAdbBackend(new AdbClient());
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
        _backend = new ProtocolAndroidAdbBackend(new AdbClient());
        Transport = "protocol";
    }

    /// <summary>
    /// Creates a bridge over an injected backend, intended for deterministic tests
    /// and hosts that already own adb server lifetime.
    /// </summary>
    public AndroidDebugBridge(IAndroidAdbBackend backend, string? adbPath = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        AdbPath = adbPath ?? "injected";
        Transport = "injected";
    }

    /// <summary>Path to the <c>adb</c> binary used to host the server.</summary>
    public string AdbPath { get; }

    /// <summary>Transport in use (<c>protocol</c> for AdvancedSharpAdbClient).</summary>
    public string Transport { get; }

    /// <summary>Lists devices via the ADB protocol (<c>host:devices-l</c>).</summary>
    public IReadOnlyList<AdbDevice> ListDevices()
    {
        return _backend.ListDevices();
    }

    /// <summary>Lists devices asynchronously through the selected backend.</summary>
    public Task<IReadOnlyList<AdbDevice>> ListDevicesAsync(
        CancellationToken cancellationToken = default) =>
        _backend.ListDevicesAsync(cancellationToken);

    /// <summary>Returns the connection state for <paramref name="serial"/> (or the default online device).</summary>
    public string GetState(string? serial = null)
    {
        var device = RequireDevice(serial);
        return device.State.ToString().ToLowerInvariant() switch
        {
            "device" => "device",
            var s => s,
        };
    }

    /// <summary>Reads a single <c>getprop</c> value over a protocol shell session.</summary>
    public string? GetProp(string propertyName, string? serial = null)
    {
        AndroidInputValidator.RequirePropertyName(propertyName);
        var result = Shell($"getprop {propertyName}", serial);
        if (!result.Ok)
            throw new InvalidOperationException($"getprop failed: {result.Diagnostic}");
        var value = result.StdOut.Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>Reads a getprop value without throwing when empty or failing.</summary>
    public string? TryGetProp(string propertyName, string? serial = null)
    {
        AndroidInputValidator.RequirePropertyName(propertyName);
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
        var memRaw = SoftShell(resolved, "cat /proc/meminfo");
        var sizeRaw = SoftShell(resolved, "wm size");
        var densRaw = SoftShell(resolved, "wm density");
        var dfRaw = SoftShell(resolved, "df -h /data /system /sdcard /storage/emulated 2>/dev/null");
        var upRaw = SoftShell(resolved, "cat /proc/uptime");
        var cpuRaw = SoftShell(resolved, "cat /proc/cpuinfo");
        var cpuCountRaw = CountProcessorLines(cpuRaw);
        var cpuHwRaw = FindCpuHardwareLine(cpuRaw);
        var androidIdRaw = SoftShell(resolved, "settings get secure android_id");
        var displayExtra = LimitLines(SoftShell(resolved, "dumpsys display"), 40);

        var extras = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(displayExtra))
        {
            extras.AppendLine("dumpsys display (first lines):");
            extras.AppendLine(displayExtra.TrimEnd());
        }

        return new AndroidDeviceInfo
        {
            Serial = resolved,
            State = device.State.ToString().ToLowerInvariant(),
            Model = Prop("ro.product.model") ?? NullIfEmpty(device.Model),
            Manufacturer = Prop("ro.product.manufacturer"),
            Brand = Prop("ro.product.brand"),
            ProductName = Prop("ro.product.name") ?? NullIfEmpty(device.Product),
            Device = Prop("ro.product.device") ?? NullIfEmpty(device.Device),
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
            CpuCoreCount = int.TryParse(cpuCountRaw, out var cores) ? cores : null,
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

    /// <summary>Collects a technical snapshot without blocking the caller thread.</summary>
    public Task<AndroidDeviceInfo> GetDeviceInfoAsync(
        string? serial = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetDeviceInfo(serial), cancellationToken);

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
            return _backend.Install(full, device.Serial, installArguments);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "install",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "install",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <summary>Installs an APK asynchronously through the protocol backend.</summary>
    public async Task<AdbOperationResult> InstallAsync(
        string apkPath,
        string? serial = null,
        CancellationToken cancellationToken = default,
        params string[] installArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apkPath);
        ArgumentNullException.ThrowIfNull(installArguments);
        var full = Path.GetFullPath(apkPath);
        if (!File.Exists(full))
        {
            return AdbOperationResult.Fail(
                "install",
                $"APK not found: {full}",
                failureKind: AndroidFailureKind.InvalidInput);
        }

        try
        {
            var device = RequireDevice(serial);
            return await _backend.InstallAsync(
                    full,
                    device.Serial,
                    installArguments,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "install",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "install",
                "APK installation cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "install",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
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
                var selection = AndroidDeviceSelector.Resolve(
                    devices,
                    new AndroidTargetOptions
                    {
                        Serial = serial,
                        RequireExplicitWhenMultiple = true,
                        RequireReady = false,
                    });
                AdbDevice? match = selection.Device;
                if (!selection.Ok && selection.Failure is { } failure)
                {
                    if (failure.Kind == AndroidFailureKind.AmbiguousDevice)
                        throw new AndroidOperationException(failure);
                    last = new AndroidOperationException(failure);
                }

                if (match is { State: AdbDeviceState.Device })
                    return match;

                if (match is not null)
                    last = new InvalidOperationException(
                        $"Device {match.Serial} is {match.State}; waiting for Device/online.");
            }
            catch (AndroidOperationException ex)
                when (ex.Failure.Kind == AndroidFailureKind.AmbiguousDevice)
            {
                throw;
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

    /// <summary>
    /// Polls asynchronously until a device is online or the timeout/cancellation
    /// boundary is reached.
    /// </summary>
    public async Task<AdbDevice> WaitForDeviceAsync(
        TimeSpan timeout,
        string? serial = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var poll = pollInterval is { } p && p > TimeSpan.Zero
            ? p
            : TimeSpan.FromMilliseconds(250);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (true)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            var devices = await ListDevicesAsync(timeoutCts.Token).ConfigureAwait(false);
            var selection = AndroidDeviceSelector.Resolve(
                devices,
                new AndroidTargetOptions
                {
                    Serial = serial,
                    RequireExplicitWhenMultiple = true,
                    RequireReady = false,
                });
            if (selection.Ok && selection.Device is { State: AdbDeviceState.Device } ready)
                return ready;
            if (selection.Failure is { Kind: AndroidFailureKind.AmbiguousDevice } failure)
                throw new AndroidOperationException(failure);

            await Task.Delay(poll, timeoutCts.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Queries whether a package is installed and reads version fields when available.</summary>
    public AndroidPackageInfo? TryGetPackageInfo(string packageName, string? serial = null)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        var path = Shell($"pm path {packageName}", serial);
        var dump = SoftShell(serial, $"dumpsys package {packageName}");
        return AndroidAppInstaller.ParsePackageInfo(
            packageName,
            path.Ok ? path.StdOut : null,
            dump);
    }

    /// <summary>Queries package information asynchronously.</summary>
    public async Task<AndroidPackageInfo?> TryGetPackageInfoAsync(
        string packageName,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        var path = await ShellAsync(
                $"pm path {packageName}",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        var dump = await ShellAsync(
                $"dumpsys package {packageName}",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        return AndroidAppInstaller.ParsePackageInfo(
            packageName,
            path.Ok ? path.StdOut : null,
            dump.Ok ? dump.StdOut : null);
    }

    /// <summary>Starts an app via <c>monkey</c> launcher intent (package main activity).</summary>
    public AdbOperationResult StartApp(string packageName, string? serial = null)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        // monkey -p <pkg> -c LAUNCHER 1 is widely available without resolving the activity class.
        var result = Shell(
            $"monkey -p {packageName} -c android.intent.category.LAUNCHER 1",
            serial);
        if (!result.Ok)
            return AdbOperationResult.Fail("startapp", result.Diagnostic, result);
        return AdbOperationResult.Success("startapp", $"Started {packageName}.", result);
    }

    /// <summary>Starts an app asynchronously through its launcher intent.</summary>
    public async Task<AdbOperationResult> StartAppAsync(
        string packageName,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        var result = await ShellAsync(
                $"monkey -p {packageName} -c android.intent.category.LAUNCHER 1",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            return AdbOperationResult.Fail(
                "startapp",
                result.Diagnostic,
                result,
                result.Cancelled
                    ? AndroidFailureKind.Cancelled
                    : AndroidFailureKind.CommandFailed);
        }

        return AdbOperationResult.Success(
            "startapp",
            $"Started {packageName}.",
            result);
    }

    /// <summary>Force-stops a package.</summary>
    public AdbOperationResult ForceStop(string packageName, string? serial = null)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        var result = Shell($"am force-stop {packageName}", serial);
        if (!result.Ok)
            return AdbOperationResult.Fail("forcestop", result.Diagnostic, result);
        return AdbOperationResult.Success("forcestop", $"Force-stopped {packageName}.", result);
    }

    /// <summary>Force-stops a package asynchronously.</summary>
    public async Task<AdbOperationResult> ForceStopAsync(
        string packageName,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        var result = await ShellAsync(
                $"am force-stop {packageName}",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            return AdbOperationResult.Fail(
                "forcestop",
                result.Diagnostic,
                result,
                result.Cancelled
                    ? AndroidFailureKind.Cancelled
                    : AndroidFailureKind.CommandFailed);
        }

        return AdbOperationResult.Success(
            "forcestop",
            $"Force-stopped {packageName}.",
            result);
    }

    /// <summary>Uninstalls a package via the ADB protocol.</summary>
    public AdbOperationResult Uninstall(string packageName, string? serial = null)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        try
        {
            var device = RequireDevice(serial);
            return _backend.Uninstall(packageName, device.Serial);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "uninstall",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "uninstall",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <summary>Uninstalls a package asynchronously.</summary>
    public async Task<AdbOperationResult> UninstallAsync(
        string packageName,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        try
        {
            var device = RequireDevice(serial);
            return await _backend.UninstallAsync(packageName, device.Serial, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "uninstall",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "uninstall",
                "Package uninstall cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "uninstall",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
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
            return _backend.Push(full, remotePath, device.Serial);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "push",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "push",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <summary>Pushes a local file asynchronously.</summary>
    public async Task<AdbOperationResult> PushAsync(
        string localPath,
        string remotePath,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        var full = Path.GetFullPath(localPath);
        if (!File.Exists(full))
            return AdbOperationResult.Fail("push", $"Local file not found: {full}", failureKind: AndroidFailureKind.InvalidInput);

        try
        {
            var device = RequireDevice(serial);
            return await _backend.PushAsync(full, remotePath, device.Serial, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "push",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "push",
                "File push cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "push",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
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
            return _backend.Pull(remotePath, full, device.Serial);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "pull",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "pull",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <summary>Pulls a remote file asynchronously.</summary>
    public async Task<AdbOperationResult> PullAsync(
        string remotePath,
        string localPath,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        var full = Path.GetFullPath(localPath);

        try
        {
            var device = RequireDevice(serial);
            return await _backend.PullAsync(remotePath, full, device.Serial, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AndroidOperationException ex)
        {
            return AdbOperationResult.Fail(
                "pull",
                ex.Failure.Message,
                failureKind: ex.Failure.Kind);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AdbOperationResult.Fail(
                "pull",
                "File pull cancelled.",
                failureKind: AndroidFailureKind.Cancelled);
        }
        catch (Exception ex)
        {
            return AdbOperationResult.Fail(
                "pull",
                ex.Message,
                failureKind: AndroidFailureKind.Transport);
        }
    }

    /// <summary>Runs a remote shell command over the ADB protocol.</summary>
    public AdbProcessResult Shell(string command, string? serial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        try
        {
            var device = RequireDevice(serial);
            return _backend.Shell(command, device.Serial);
        }
        catch (AndroidOperationException ex)
        {
            return new AdbProcessResult(1, "", ex.Failure.Message);
        }
        catch (Exception ex)
        {
            return new AdbProcessResult(1, "", ex.Message);
        }
    }

    /// <summary>Runs a remote shell command asynchronously.</summary>
    public async Task<AdbProcessResult> ShellAsync(
        string command,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        try
        {
            var device = RequireDevice(serial);
            return await _backend.ShellAsync(command, device.Serial, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AndroidOperationException ex)
        {
            return new AdbProcessResult(1, "", ex.Failure.Message)
            {
                Cancelled = ex.Failure.Kind == AndroidFailureKind.Cancelled,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new AdbProcessResult(1, "", "ADB shell operation cancelled.")
            {
                Cancelled = true,
            };
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
        var resolved = AndroidDeviceSelector.ResolveSerial(serial);
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

    private AdbDevice RequireDevice(string? serial)
    {
        var selection = AndroidDeviceSelector.Resolve(
            ListDevices(),
            new AndroidTargetOptions { Serial = serial });
        if (selection.Ok && selection.Device is { } device)
            return device;
        throw new AndroidOperationException(selection.Failure!);
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

    private static string? CountProcessorLines(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var count = raw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line => line.StartsWith("processor", StringComparison.OrdinalIgnoreCase));
        return count == 0 ? null : count.ToString(CultureInfo.InvariantCulture);
    }

    private static string? FindCpuHardwareLine(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return raw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line =>
                line.StartsWith("Hardware:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("model name:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Processor:", StringComparison.OrdinalIgnoreCase));
    }

    private static string? LimitLines(string? raw, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(raw) || maxLines <= 0)
            return raw;

        return string.Join(
            Environment.NewLine,
            raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Take(maxLines));
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
