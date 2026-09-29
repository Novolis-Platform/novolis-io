using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.IO.Mobile.Android;

/// <summary>Options for finite logcat capture.</summary>
public sealed class AndroidLogcatOptions
{
    /// <summary>Target serial.</summary>
    public string? Serial { get; init; }

    /// <summary>Only retain lines containing this package text.</summary>
    public string? PackageName { get; init; }

    /// <summary>Optional adb logcat time selector such as <c>-t 200</c>.</summary>
    public int? LastLines { get; init; } = 500;

    /// <summary>Whether to clear the log buffer before capture.</summary>
    public bool ClearBeforeCapture { get; init; }

    /// <summary>Finite operation timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>Finite logcat capture output.</summary>
public sealed record AndroidLogcatCapture(
    bool Ok,
    string Text,
    AdbProcessResult Process,
    AndroidFailure? Failure = null);

/// <summary>Screenshot capture output.</summary>
public sealed record AndroidScreenshotCapture(
    bool Ok,
    string Path,
    AdbBinaryProcessResult Process,
    AndroidFailure? Failure = null);

/// <summary>UIAutomator hierarchy capture output.</summary>
public sealed record AndroidUiDumpCapture(
    bool Ok,
    string? Xml,
    string? Path,
    AdbProcessResult Process,
    AndroidFailure? Failure = null);

/// <summary>Result of a diagnostic evidence bundle.</summary>
public sealed record AndroidDiagnosticBundleResult(
    bool Ok,
    string Directory,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Warnings,
    AndroidFailure? Failure = null);

/// <summary>Common device evidence operations for tools and UI hosts.</summary>
public interface IAndroidDiagnostics
{
    /// <summary>Captures finite, optionally filtered logcat output.</summary>
    Task<AndroidLogcatCapture> CaptureLogcatAsync(
        AndroidLogcatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Follows logcat until cancellation.</summary>
    IAsyncEnumerable<string> FollowLogcatAsync(
        AndroidLogcatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Captures a PNG screenshot.</summary>
    Task<AndroidScreenshotCapture> CaptureScreenshotAsync(
        string outputPath,
        string? serial = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>Captures and optionally writes UIAutomator XML.</summary>
    Task<AndroidUiDumpCapture> DumpUiAsync(
        string? outputPath = null,
        string? serial = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>Collects redacted device evidence into one directory.</summary>
    Task<AndroidDiagnosticBundleResult> CollectBundleAsync(
        string outputDirectory,
        string? packageName = null,
        string? serial = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Host-side Android diagnostics and controlled device actions.
/// The command runner is used only for ADB verbs that have no protocol façade.
/// </summary>
public sealed class AndroidDeviceDiagnostics : IAndroidDiagnostics
{
    private readonly AndroidDebugBridge _adb;
    private readonly IAdbProcessRunner _runner;

    /// <summary>Creates a diagnostics service.</summary>
    public AndroidDeviceDiagnostics(
        AndroidDebugBridge adb,
        IAdbProcessRunner? runner = null)
    {
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));
        _runner = runner ?? new ProcessAdbRunner(adb.AdbPath);
    }

    /// <summary>Bridge used for protocol-backed actions.</summary>
    public AndroidDebugBridge Adb => _adb;

    /// <inheritdoc />
    public async Task<AndroidLogcatCapture> CaptureLogcatAsync(
        AndroidLogcatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AndroidLogcatOptions();
        if (options.Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.Timeout));
        if (options.LastLines is <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.LastLines));
        if (options.PackageName is not null)
            AndroidInputValidator.RequirePackageName(options.PackageName);

        if (options.ClearBeforeCapture)
        {
            var clear = await RunAdbAsync(
                    options.Serial,
                    options.Timeout,
                    cancellationToken,
                    "logcat",
                    "-c")
                .ConfigureAwait(false);
            if (!clear.Ok)
            {
                return new AndroidLogcatCapture(
                    false,
                    "",
                    clear,
                    FailureFromProcess(clear, options.Serial, "logcat -c"));
            }
        }

        var args = new List<string> { "logcat", "-d", "-v", "threadtime" };
        if (options.LastLines is int last)
        {
            args.Add("-t");
            args.Add(last.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var result = await RunAdbAsync(
                options.Serial,
                options.Timeout,
                cancellationToken,
                args.ToArray())
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            return new AndroidLogcatCapture(
                false,
                "",
                result,
                FailureFromProcess(result, options.Serial, "logcat"));
        }

        var text = result.StdOut;
        if (!string.IsNullOrWhiteSpace(options.PackageName))
        {
            text = string.Join(
                Environment.NewLine,
                text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.Contains(options.PackageName, StringComparison.OrdinalIgnoreCase)));
        }

        return new AndroidLogcatCapture(
            true,
            AndroidOutputRedactor.Redact(text),
            result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> FollowLogcatAsync(
        AndroidLogcatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new AndroidLogcatOptions();
        if (options.PackageName is not null)
            AndroidInputValidator.RequirePackageName(options.PackageName);

        var process = StartAdbProcess(
            options.Serial,
            "logcat",
            "-v",
            "threadtime");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (line is null)
                    break;
                if (!string.IsNullOrWhiteSpace(options.PackageName)
                    && !line.Contains(options.PackageName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return AndroidOutputRedactor.Redact(line);
            }
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have exited during cancellation.
            }

            process.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<AndroidScreenshotCapture> CaptureScreenshotAsync(
        string outputPath,
        string? serial = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var result = await RunAdbBinaryAsync(
                serial,
                timeout ?? TimeSpan.FromSeconds(30),
                cancellationToken,
                "exec-out",
                "screencap",
                "-p")
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            return new AndroidScreenshotCapture(
                false,
                full,
                result,
                FailureFromBinaryProcess(result, serial, "screencap"));
        }

        if (!IsPng(result.StdOut))
        {
            return new AndroidScreenshotCapture(
                false,
                full,
                result,
                new AndroidFailure(
                    AndroidFailureKind.CommandFailed,
                    "screencap returned data that is not a PNG.",
                    serial,
                    "screencap"));
        }

        await File.WriteAllBytesAsync(full, result.StdOut, cancellationToken)
            .ConfigureAwait(false);
        return new AndroidScreenshotCapture(true, full, result);
    }

    /// <inheritdoc />
    public async Task<AndroidUiDumpCapture> DumpUiAsync(
        string? outputPath = null,
        string? serial = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAdbAsync(
                serial,
                timeout ?? TimeSpan.FromSeconds(30),
                cancellationToken,
                "exec-out",
                "uiautomator",
                "dump",
                "/dev/tty")
            .ConfigureAwait(false);
        var xml = ExtractUiXml(result.StdOut);
        var full = string.IsNullOrWhiteSpace(outputPath)
            ? null
            : Path.GetFullPath(outputPath);

        if (!result.Ok || string.IsNullOrWhiteSpace(xml))
        {
            return new AndroidUiDumpCapture(
                false,
                xml,
                full,
                result,
                FailureFromProcess(
                    result,
                    serial,
                    "uiautomator dump",
                    "UIAutomator did not return a hierarchy XML document."));
        }

        if (full is not null)
        {
            var directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(full, xml, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
        }

        return new AndroidUiDumpCapture(true, xml, full, result);
    }

    /// <summary>Performs a controlled tap action.</summary>
    public Task<AdbProcessResult> TapAsync(
        int x,
        int y,
        string? serial = null,
        CancellationToken cancellationToken = default) =>
        ShellInputAsync(
            $"input tap {x} {y}",
            serial,
            cancellationToken);

    /// <summary>Performs a controlled swipe action.</summary>
    public Task<AdbProcessResult> SwipeAsync(
        int startX,
        int startY,
        int endX,
        int endY,
        int? durationMs = null,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        var duration = durationMs is null
            ? ""
            : $" {Math.Max(1, durationMs.Value)}";
        return ShellInputAsync(
            $"input swipe {startX} {startY} {endX} {endY}{duration}",
            serial,
            cancellationToken);
    }

    /// <summary>Sends one Android key event.</summary>
    public Task<AdbProcessResult> KeyEventAsync(
        string key,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)
            || key.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
        {
            throw new ArgumentException("Invalid Android key event.", nameof(key));
        }

        return ShellInputAsync($"input keyevent {key}", serial, cancellationToken);
    }

    /// <summary>Injects text after quoting it for the remote shell.</summary>
    public Task<AdbProcessResult> TextAsync(
        string text,
        string? serial = null,
        CancellationToken cancellationToken = default) =>
        ShellInputAsync(
            $"input text {AndroidInputValidator.QuoteShellArgument(text)}",
            serial,
            cancellationToken);

    /// <summary>Clears one package's application data.</summary>
    public Task<AdbProcessResult> ClearDataAsync(
        string packageName,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        return _adb.ShellAsync(
            $"pm clear {packageName}",
            serial,
            cancellationToken);
    }

    /// <summary>Grants one runtime permission to a package.</summary>
    public Task<AdbProcessResult> GrantPermissionAsync(
        string packageName,
        string permission,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        RequirePermission(permission);
        return _adb.ShellAsync(
            $"pm grant {packageName} {permission}",
            serial,
            cancellationToken);
    }

    /// <summary>Revokes one runtime permission from a package.</summary>
    public Task<AdbProcessResult> RevokePermissionAsync(
        string packageName,
        string permission,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        AndroidInputValidator.RequirePackageName(packageName);
        RequirePermission(permission);
        return _adb.ShellAsync(
            $"pm revoke {packageName} {permission}",
            serial,
            cancellationToken);
    }

    /// <summary>Creates a host-to-device port forward.</summary>
    public Task<AdbProcessResult> ForwardAsync(
        string local,
        string remote,
        string? serial = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        RunAdbAsync(
            serial,
            timeout ?? TimeSpan.FromSeconds(15),
            cancellationToken,
            "forward",
            local,
            remote);

    /// <summary>Creates a device-to-host reverse port mapping.</summary>
    public Task<AdbProcessResult> ReverseAsync(
        string remote,
        string local,
        string? serial = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        RunAdbAsync(
            serial,
            timeout ?? TimeSpan.FromSeconds(15),
            cancellationToken,
            "reverse",
            remote,
            local);

    /// <inheritdoc />
    public async Task<AndroidDiagnosticBundleResult> CollectBundleAsync(
        string outputDirectory,
        string? packageName = null,
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        var files = new List<string>();
        var warnings = new List<string>();

        try
        {
            var infoPath = Path.Combine(directory, "device-info.txt");
            var info = AndroidOutputRedactor.RedactDeviceInfo(
                    await _adb.GetDeviceInfoAsync(serial, cancellationToken)
                        .ConfigureAwait(false))
                .FormatReport();
            await File.WriteAllTextAsync(
                    infoPath,
                    AndroidOutputRedactor.Redact(info),
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
            files.Add(infoPath);

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                AndroidInputValidator.RequirePackageName(packageName);
                var package = await _adb.TryGetPackageInfoAsync(
                        packageName,
                        serial,
                        cancellationToken)
                    .ConfigureAwait(false);
                var packagePath = Path.Combine(directory, "package.json");
                var packagePayload = package is null
                    ? null
                    : new
                    {
                        package.PackageName,
                        package.VersionName,
                        package.VersionCode,
                        package.IsInstalled,
                    };
                await File.WriteAllTextAsync(
                        packagePath,
                        JsonSerializer.Serialize(packagePayload, new JsonSerializerOptions { WriteIndented = true }),
                        Encoding.UTF8,
                        cancellationToken)
                    .ConfigureAwait(false);
                files.Add(packagePath);
            }

            var logcat = await CaptureLogcatAsync(
                    new AndroidLogcatOptions
                    {
                        Serial = serial,
                        PackageName = packageName,
                        LastLines = 500,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            var logPath = Path.Combine(directory, "logcat.txt");
            await File.WriteAllTextAsync(logPath, logcat.Text, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            files.Add(logPath);
            if (!logcat.Ok)
                warnings.Add(logcat.Failure?.Message ?? "logcat capture failed");

            var screenshot = await CaptureScreenshotAsync(
                    Path.Combine(directory, "screenshot.png"),
                    serial,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (screenshot.Ok)
                files.Add(screenshot.Path);
            else
                warnings.Add(screenshot.Failure?.Message ?? "screenshot capture failed");

            var ui = await DumpUiAsync(
                    Path.Combine(directory, "ui.xml"),
                    serial,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (ui.Ok && ui.Path is not null)
                files.Add(ui.Path);
            else
                warnings.Add(ui.Failure?.Message ?? "UI hierarchy capture failed");

            return new AndroidDiagnosticBundleResult(
                true,
                directory,
                files,
                warnings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new AndroidDiagnosticBundleResult(
                false,
                directory,
                files,
                warnings,
                new AndroidFailure(
                    AndroidFailureKind.Cancelled,
                    "Diagnostic collection cancelled.",
                    serial));
        }
        catch (Exception ex)
        {
            return new AndroidDiagnosticBundleResult(
                false,
                directory,
                files,
                warnings,
                new AndroidFailure(
                    AndroidFailureKind.Unknown,
                    ex.Message,
                    serial,
                    Exception: ex));
        }
    }

    private Task<AdbProcessResult> ShellInputAsync(
        string command,
        string? serial,
        CancellationToken cancellationToken) =>
        _adb.ShellAsync(command, serial, cancellationToken);

    private Task<AdbProcessResult> RunAdbAsync(
        string? serial,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params string[] args) =>
        _runner.RunAsync(timeout, cancellationToken, WithSerial(serial, args));

    private Task<AdbBinaryProcessResult> RunAdbBinaryAsync(
        string? serial,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params string[] args) =>
        _runner.RunBinaryAsync(timeout, cancellationToken, WithSerial(serial, args));

    private Process StartAdbProcess(string? serial, params string[] args)
    {
        var psi = new ProcessStartInfo(_runner.AdbPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in WithSerial(serial, args))
            psi.ArgumentList.Add(arg);
        return Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start adb at '{_runner.AdbPath}'.");
    }

    private static string[] WithSerial(string? serial, string[] args)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return args;
        return ["-s", serial.Trim(), .. args];
    }

    private static AndroidFailure FailureFromProcess(
        AdbProcessResult result,
        string? serial,
        string command,
        string? fallback = null) =>
        new(
            result.Cancelled
                ? AndroidFailureKind.Cancelled
                : result.TimedOut
                    ? AndroidFailureKind.Timeout
                    : AndroidFailureKind.CommandFailed,
            string.IsNullOrWhiteSpace(result.Diagnostic) ? fallback ?? $"{command} failed." : result.Diagnostic,
            serial,
            command,
            result.ExitCode);

    private static AndroidFailure FailureFromBinaryProcess(
        AdbBinaryProcessResult result,
        string? serial,
        string command) =>
        new(
            result.Cancelled
                ? AndroidFailureKind.Cancelled
                : result.TimedOut
                    ? AndroidFailureKind.Timeout
                    : AndroidFailureKind.CommandFailed,
            string.IsNullOrWhiteSpace(result.Diagnostic)
                ? $"{command} failed."
                : result.Diagnostic,
            serial,
            command,
            result.ExitCode);

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 8
        && bytes[0] == 0x89
        && bytes[1] == 0x50
        && bytes[2] == 0x4E
        && bytes[3] == 0x47
        && bytes[4] == 0x0D
        && bytes[5] == 0x0A
        && bytes[6] == 0x1A
        && bytes[7] == 0x0A;

    private static string? ExtractUiXml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var start = text.IndexOf("<hierarchy", StringComparison.Ordinal);
        var end = text.LastIndexOf("</hierarchy>", StringComparison.Ordinal);
        if (start < 0 || end < start)
            return null;
        end += "</hierarchy>".Length;
        return text[start..end];
    }

    private static void RequirePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)
            || permission.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '_')))
        {
            throw new ArgumentException("Invalid Android permission.", nameof(permission));
        }
    }
}

/// <summary>Redacts common credentials from Android evidence text.</summary>
public static partial class AndroidOutputRedactor
{
    /// <summary>Redacts bearer, key, secret, password, and token assignments.</summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";
        return SecretAssignmentRegex().Replace(text, "$1[REDACTED]");
    }

    /// <summary>
    /// Creates a device report suitable for sharing by removing transport and
    /// hardware identifiers while preserving diagnostic facts.
    /// </summary>
    public static AndroidDeviceInfo RedactDeviceInfo(AndroidDeviceInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return new AndroidDeviceInfo
        {
            Serial = RedactIdentifier(info.Serial),
            State = info.State,
            Model = info.Model,
            Manufacturer = info.Manufacturer,
            Brand = info.Brand,
            ProductName = info.ProductName,
            Device = info.Device,
            Board = info.Board,
            Hardware = info.Hardware,
            AndroidVersion = info.AndroidVersion,
            SdkVersion = info.SdkVersion,
            FirstApiLevel = info.FirstApiLevel,
            SecurityPatch = info.SecurityPatch,
            BuildDisplay = info.BuildDisplay,
            BuildId = info.BuildId,
            BuildType = info.BuildType,
            BuildTags = info.BuildTags,
            Fingerprint = RedactOptionalIdentifier(info.Fingerprint),
            Bootloader = info.Bootloader,
            Baseband = info.Baseband,
            HardwareSerial = RedactOptionalIdentifier(info.HardwareSerial),
            AndroidId = RedactOptionalIdentifier(info.AndroidId),
            Abi = info.Abi,
            AbiList = info.AbiList,
            CpuCoreCount = info.CpuCoreCount,
            CpuHardware = info.CpuHardware,
            Timezone = info.Timezone,
            UptimeSeconds = info.UptimeSeconds,
            Battery = info.Battery,
            Memory = info.Memory,
            Display = info.Display,
            Storage = info.Storage
                .Select(storage => storage with { MountedOn = RedactIdentifier(storage.MountedOn) })
                .ToArray(),
            RawExtras = RedactDeviceIdentifiers(Redact(info.RawExtras)),
        };
    }

    private static string RedactIdentifier(string value) =>
        string.IsNullOrWhiteSpace(value) ? value : "[REDACTED]";

    private static string? RedactOptionalIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? value : "[REDACTED]";

    private static string? RedactDeviceIdentifiers(string? value) =>
        string.IsNullOrEmpty(value)
            ? value
            : DeviceIdentifierRegex().Replace(value, "$1[REDACTED]$3");

    [GeneratedRegex(
        @"(?i)(\b(?:uniqueId|mPhysicalDisplayId|mDisplayToken)\s*[=:]\s*['""]?)([^,'""}\s]+)(['""]?)",
        RegexOptions.CultureInvariant)]
    private static partial Regex DeviceIdentifierRegex();

    [GeneratedRegex(
        @"(?i)(\b(?:authorization|access[_-]?token|refresh[_-]?token|client[_-]?secret|api[_-]?key|password|secret|token)\b\s*[:=]\s*(?:bearer\s+)?)([^\s,;""']+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentRegex();
}
