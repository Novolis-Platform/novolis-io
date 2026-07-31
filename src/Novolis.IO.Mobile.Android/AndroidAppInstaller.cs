using System.Globalization;
using System.Text.RegularExpressions;

namespace Novolis.IO.Mobile.Android;

/// <summary>
/// Helper workflow: validate APK → wait for ready device → install → optional verify / launch.
/// </summary>
public sealed class AndroidAppInstaller
{
    private static readonly Regex VersionNameRegex = new(@"versionName=([^\s]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VersionCodeRegex = new(@"versionCode=(\d+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PmPathRegex = new(@"package:(.+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly AndroidDebugBridge _adb;

    /// <summary>Creates an installer bound to <paramref name="adb"/>.</summary>
    public AndroidAppInstaller(AndroidDebugBridge adb) =>
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));

    /// <summary>Underlying bridge.</summary>
    public AndroidDebugBridge Adb => _adb;

    /// <summary>Validates a local APK without touching a device.</summary>
    public ApkValidationResult ValidateApk(string apkPath, ApkInstallOptions? options = null) =>
        ApkValidator.Validate(apkPath, options);

    /// <summary>
    /// Waits until a device is in <see cref="AdbDeviceState.Device"/> state.
    /// </summary>
    public AdbDevice WaitForReadyDevice(TimeSpan timeout, string? serial = null, TimeSpan? pollInterval = null) =>
        _adb.WaitForDevice(timeout, serial, pollInterval);

    /// <summary>Returns package info when installed; otherwise null.</summary>
    public AndroidPackageInfo? TryGetPackage(string packageName, string? serial = null) =>
        _adb.TryGetPackageInfo(packageName, serial);

    /// <summary>Whether <paramref name="packageName"/> is installed.</summary>
    public bool IsPackageInstalled(string packageName, string? serial = null) =>
        TryGetPackage(packageName, serial)?.IsInstalled == true;

    /// <summary>
    /// Validates the APK, waits for a ready device, installs, optionally verifies and launches.
    /// </summary>
    public ApkInstallResult Install(string apkPath, ApkInstallOptions? options = null)
    {
        options ??= new ApkInstallOptions();
        var validation = ApkValidator.Validate(apkPath, options);
        if (!validation.Ok)
        {
            return new ApkInstallResult(
                false,
                string.Join("; ", validation.Errors),
                validation.ApkPath,
                options.Serial,
                validation: validation);
        }

        AdbDevice device;
        try
        {
            device = _adb.WaitForDevice(options.DeviceWaitTimeout, options.Serial);
        }
        catch (Exception ex)
        {
            return new ApkInstallResult(
                false,
                ex.Message,
                validation.ApkPath,
                options.Serial,
                validation: validation);
        }

        if (device.State != AdbDeviceState.Device)
        {
            return new ApkInstallResult(
                false,
                $"Device {device.Serial} is {device.State} (need Device/online).",
                validation.ApkPath,
                device.Serial,
                validation: validation);
        }

        var args = BuildInstallArgs(options);
        var install = _adb.Install(validation.ApkPath, device.Serial, args);
        if (!install.Ok)
        {
            return new ApkInstallResult(
                false,
                install.Message,
                validation.ApkPath,
                device.Serial,
                validation: validation,
                install: install);
        }

        AndroidPackageInfo? package = null;
        if (options.VerifyInstalled && !string.IsNullOrWhiteSpace(options.ExpectedPackageName))
        {
            // Brief settle for package manager.
            Thread.Sleep(200);
            package = _adb.TryGetPackageInfo(options.ExpectedPackageName, device.Serial);
            if (package is null || !package.IsInstalled)
            {
                return new ApkInstallResult(
                    false,
                    $"Install reported success but package '{options.ExpectedPackageName}' was not found.",
                    validation.ApkPath,
                    device.Serial,
                    package,
                    validation,
                    install);
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.ExpectedPackageName))
        {
            package = _adb.TryGetPackageInfo(options.ExpectedPackageName, device.Serial);
        }

        if (options.LaunchAfterInstall)
        {
            if (string.IsNullOrWhiteSpace(options.ExpectedPackageName))
            {
                return new ApkInstallResult(
                    false,
                    "LaunchAfterInstall requires ExpectedPackageName.",
                    validation.ApkPath,
                    device.Serial,
                    package,
                    validation,
                    install);
            }

            var launch = _adb.StartApp(options.ExpectedPackageName, device.Serial);
            if (!launch.Ok)
            {
                return new ApkInstallResult(
                    false,
                    $"Installed but launch failed: {launch.Message}",
                    validation.ApkPath,
                    device.Serial,
                    package,
                    validation,
                    install);
            }
        }

        var msg = package is { IsInstalled: true }
            ? $"Installed {Path.GetFileName(validation.ApkPath)} → {package.PackageName}" +
              (package.VersionName is { } vn ? $" {vn}" : "") +
              (package.VersionCode is { } vc ? $" ({vc})" : "") +
              $" on {device.Serial}."
            : $"Installed {Path.GetFileName(validation.ApkPath)} on {device.Serial}.";

        return new ApkInstallResult(true, msg, validation.ApkPath, device.Serial, package, validation, install);
    }

    /// <summary>Builds <c>adb install</c> flag list from options.</summary>
    public static string[] BuildInstallArgs(ApkInstallOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var args = new List<string>();
        if (options.Reinstall)
            args.Add("-r");
        if (options.GrantPermissions)
            args.Add("-g");
        if (options.AllowDowngrade)
            args.Add("-d");
        return args.ToArray();
    }

    /// <summary>Parses <c>pm path</c> / dumpsys snippets into <see cref="AndroidPackageInfo"/>.</summary>
    public static AndroidPackageInfo? ParsePackageInfo(string packageName, string? pmPathOutput, string? dumpsysSnippet = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        string? apkPath = null;
        if (!string.IsNullOrWhiteSpace(pmPathOutput))
        {
            var match = PmPathRegex.Match(pmPathOutput);
            if (match.Success)
                apkPath = match.Groups[1].Value.Trim();
        }

        if (apkPath is null && string.IsNullOrWhiteSpace(dumpsysSnippet))
            return null;

        string? versionName = null;
        int? versionCode = null;
        if (!string.IsNullOrWhiteSpace(dumpsysSnippet))
        {
            var vn = VersionNameRegex.Match(dumpsysSnippet);
            if (vn.Success)
                versionName = vn.Groups[1].Value.Trim().Trim('"');
            var vc = VersionCodeRegex.Match(dumpsysSnippet);
            if (vc.Success && int.TryParse(vc.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                versionCode = code;
        }

        if (apkPath is null && versionName is null && versionCode is null)
            return null;

        return new AndroidPackageInfo
        {
            PackageName = packageName,
            ApkPath = apkPath,
            VersionName = versionName,
            VersionCode = versionCode,
        };
    }
}
