namespace Novolis.IO.Mobile.Android;

/// <summary>Options for validating and installing an APK.</summary>
public sealed class ApkInstallOptions
{
    /// <summary>Target device serial; null uses <c>ANDROID_SERIAL</c> or the first online device.</summary>
    public string? Serial { get; init; }

    /// <summary>Pass <c>-r</c> (replace existing). Default true.</summary>
    public bool Reinstall { get; init; } = true;

    /// <summary>Pass <c>-g</c> (grant all runtime permissions).</summary>
    public bool GrantPermissions { get; init; }

    /// <summary>Pass <c>-d</c> (allow version code downgrade).</summary>
    public bool AllowDowngrade { get; init; }

    /// <summary>When set, post-install verification requires this package id.</summary>
    public string? ExpectedPackageName { get; init; }

    /// <summary>After install, confirm the package is present when <see cref="ExpectedPackageName"/> is set.</summary>
    public bool VerifyInstalled { get; init; } = true;

    /// <summary>Launch the package after a successful install (needs <see cref="ExpectedPackageName"/>).</summary>
    public bool LaunchAfterInstall { get; init; }

    /// <summary>How long to wait for a ready device before install.</summary>
    public TimeSpan DeviceWaitTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>Minimum APK size in bytes (guards empty/truncated files). Default 1 KiB.</summary>
    public long MinApkBytes { get; init; } = 1024;

    /// <summary>Optional maximum APK size in bytes.</summary>
    public long? MaxApkBytes { get; init; }

    /// <summary>Require <c>AndroidManifest.xml</c> and <c>classes.dex</c> zip entries. Default true.</summary>
    public bool RequireApkEntries { get; init; } = true;
}

/// <summary>Outcome of local APK validation (no device required).</summary>
public sealed class ApkValidationResult
{
    /// <summary>Creates a validation result.</summary>
    public ApkValidationResult(bool ok, string apkPath, long sizeBytes, IReadOnlyList<string> errors, IReadOnlyList<string>? warnings = null)
    {
        Ok = ok;
        ApkPath = apkPath;
        SizeBytes = sizeBytes;
        Errors = errors;
        Warnings = warnings ?? [];
    }

    /// <summary>Whether validation passed.</summary>
    public bool Ok { get; }

    /// <summary>Resolved full path.</summary>
    public string ApkPath { get; }

    /// <summary>File size in bytes (0 when missing).</summary>
    public long SizeBytes { get; }

    /// <summary>Blocking problems.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Non-blocking notes.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Success factory.</summary>
    public static ApkValidationResult Success(string apkPath, long sizeBytes, IReadOnlyList<string>? warnings = null) =>
        new(true, apkPath, sizeBytes, [], warnings);

    /// <summary>Failure factory.</summary>
    public static ApkValidationResult Fail(string apkPath, long sizeBytes, params string[] errors) =>
        new(false, apkPath, sizeBytes, errors);
}

/// <summary>Installed package snapshot from the device.</summary>
public sealed class AndroidPackageInfo
{
    /// <summary>Package id (e.g. <c>com.novolis.booksmobile</c>).</summary>
    public required string PackageName { get; init; }

    /// <summary><c>pm path</c> APK location when installed.</summary>
    public string? ApkPath { get; init; }

    /// <summary><c>versionName</c> when available.</summary>
    public string? VersionName { get; init; }

    /// <summary><c>versionCode</c> when available.</summary>
    public int? VersionCode { get; init; }

    /// <summary>Whether an APK path was reported.</summary>
    public bool IsInstalled => !string.IsNullOrWhiteSpace(ApkPath);
}

/// <summary>Outcome of <see cref="AndroidAppInstaller.Install"/>.</summary>
public sealed class ApkInstallResult
{
    /// <summary>Creates an install result.</summary>
    public ApkInstallResult(
        bool ok,
        string message,
        string apkPath,
        string? serial = null,
        AndroidPackageInfo? package = null,
        ApkValidationResult? validation = null,
        AdbOperationResult? install = null)
    {
        Ok = ok;
        Message = message;
        ApkPath = apkPath;
        Serial = serial;
        Package = package;
        Validation = validation;
        Install = install;
    }

    /// <summary>Whether the workflow succeeded.</summary>
    public bool Ok { get; }

    /// <summary>Human-readable summary.</summary>
    public string Message { get; }

    /// <summary>APK path used.</summary>
    public string ApkPath { get; }

    /// <summary>Device serial used.</summary>
    public string? Serial { get; }

    /// <summary>Package info after verify (when requested).</summary>
    public AndroidPackageInfo? Package { get; }

    /// <summary>Local validation result.</summary>
    public ApkValidationResult? Validation { get; }

    /// <summary>Underlying protocol install result.</summary>
    public AdbOperationResult? Install { get; }
}
