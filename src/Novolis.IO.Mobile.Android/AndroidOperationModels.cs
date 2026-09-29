namespace Novolis.IO.Mobile.Android;

/// <summary>Stable categories for failures reported by Android host operations.</summary>
public enum AndroidFailureKind
{
    /// <summary>The failure does not match a more specific category.</summary>
    Unknown = 0,

    /// <summary>The adb executable could not be located.</summary>
    MissingAdb,

    /// <summary>The local adb server could not be started or reached.</summary>
    ServerUnavailable,

    /// <summary>No adb devices are connected.</summary>
    NoDevices,

    /// <summary>The requested serial is not present.</summary>
    DeviceNotFound,

    /// <summary>More than one ready device exists and no serial was supplied.</summary>
    AmbiguousDevice,

    /// <summary>The handset has not authorized this computer.</summary>
    Unauthorized,

    /// <summary>The transport is present but offline.</summary>
    Offline,

    /// <summary>The host does not have permission to use the transport.</summary>
    NoPermissions,

    /// <summary>The device transport was lost during an operation.</summary>
    Transport,

    /// <summary>The remote command returned a non-zero status.</summary>
    CommandFailed,

    /// <summary>The operation exceeded its configured time limit.</summary>
    Timeout,

    /// <summary>The caller cancelled the operation.</summary>
    Cancelled,

    /// <summary>The local artifact or input was invalid.</summary>
    InvalidInput,

    /// <summary>The package or artifact identity did not match the request.</summary>
    VerificationFailed,

    /// <summary>The selected Android version does not support the operation.</summary>
    NotSupported,

    /// <summary>The device refused the requested action.</summary>
    PermissionDenied,
}

/// <summary>Structured failure data that can be rendered by a UI or CLI.</summary>
public sealed record AndroidFailure(
    AndroidFailureKind Kind,
    string Message,
    string? Serial = null,
    string? Command = null,
    int? ExitCode = null,
    Exception? Exception = null)
{
    /// <summary>Creates a failure from a device state.</summary>
    public static AndroidFailure ForDevice(AdbDevice device) =>
        new(
            device.State switch
            {
                AdbDeviceState.Unauthorized => AndroidFailureKind.Unauthorized,
                AdbDeviceState.Offline => AndroidFailureKind.Offline,
                AdbDeviceState.NoPermissions => AndroidFailureKind.NoPermissions,
                _ => AndroidFailureKind.Transport,
            },
            $"Device '{device.Serial}' is {device.State}; a ready 'device' state is required.",
            device.Serial);
}

/// <summary>Exception carrying a stable Android failure category.</summary>
public sealed class AndroidOperationException : InvalidOperationException
{
    /// <summary>Creates an exception for <paramref name="failure"/>.</summary>
    public AndroidOperationException(AndroidFailure failure)
        : base(failure?.Message, failure?.Exception)
    {
        Failure = failure ?? throw new ArgumentNullException(nameof(failure));
    }

    /// <summary>Structured failure represented by this exception.</summary>
    public AndroidFailure Failure { get; }
}

/// <summary>Rules used when resolving an adb device target.</summary>
public sealed class AndroidTargetOptions
{
    /// <summary>Explicit serial. When null, ANDROID_SERIAL is considered.</summary>
    public string? Serial { get; init; }

    /// <summary>Whether multiple ready devices require an explicit serial.</summary>
    public bool RequireExplicitWhenMultiple { get; init; } = true;

    /// <summary>Whether a non-ready matching serial should be returned as a failure.</summary>
    public bool RequireReady { get; init; } = true;
}

/// <summary>Pure result of resolving a target from a device list.</summary>
public sealed record AndroidTargetResolution(
    bool Ok,
    AdbDevice? Device,
    AndroidFailure? Failure)
{
    /// <summary>Creates a successful resolution.</summary>
    public static AndroidTargetResolution Success(AdbDevice device) =>
        new(true, device, null);

    /// <summary>Creates a failed resolution.</summary>
    public static AndroidTargetResolution Fail(AndroidFailure failure) =>
        new(false, null, failure);
}

/// <summary>Deterministic device-target selection shared by the library, CLI, and UI.</summary>
public static class AndroidDeviceSelector
{
    /// <summary>
    /// Resolves a device without silently choosing among multiple ready devices.
    /// </summary>
    public static AndroidTargetResolution Resolve(
        IReadOnlyList<AdbDevice> devices,
        AndroidTargetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(devices);
        options ??= new AndroidTargetOptions();

        var requested = ResolveSerial(options.Serial);
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var match = devices.FirstOrDefault(
                d => string.Equals(d.Serial, requested, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return AndroidTargetResolution.Fail(
                    new AndroidFailure(
                        AndroidFailureKind.DeviceNotFound,
                        $"Device '{requested}' was not found.",
                        requested));
            }

            if (options.RequireReady && match.State != AdbDeviceState.Device)
            {
                return AndroidTargetResolution.Fail(AndroidFailure.ForDevice(match));
            }

            return AndroidTargetResolution.Success(match);
        }

        if (devices.Count == 0)
        {
            return AndroidTargetResolution.Fail(
                new AndroidFailure(
                    AndroidFailureKind.NoDevices,
                    "No adb devices are connected."));
        }

        var ready = devices
            .Where(d => d.State == AdbDeviceState.Device)
            .ToArray();
        if (ready.Length == 0)
        {
            var first = devices[0];
            return AndroidTargetResolution.Fail(AndroidFailure.ForDevice(first));
        }

        if (options.RequireExplicitWhenMultiple && ready.Length > 1)
        {
            var serials = string.Join(", ", ready.Select(d => d.Serial));
            return AndroidTargetResolution.Fail(
                new AndroidFailure(
                    AndroidFailureKind.AmbiguousDevice,
                    $"Multiple ready devices are connected ({serials}); specify a serial.",
                    Command: "devices"));
        }

        return AndroidTargetResolution.Success(ready[0]);
    }

    /// <summary>Returns an explicit serial or the configured ANDROID_SERIAL.</summary>
    public static string? ResolveSerial(string? serial) =>
        !string.IsNullOrWhiteSpace(serial)
            ? serial.Trim()
            : Environment.GetEnvironmentVariable("ANDROID_SERIAL") is { } env
                && !string.IsNullOrWhiteSpace(env)
                ? env.Trim()
                : null;
}

/// <summary>Common limits for cancellable Android operations.</summary>
public sealed class AndroidOperationOptions
{
    /// <summary>Default maximum time for a finite operation.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>Maximum captured text for one operation.</summary>
    public int MaxOutputCharacters { get; init; } = 512 * 1024;

    /// <summary>Validates this option set.</summary>
    public void Validate()
    {
        if (Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        if (MaxOutputCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxOutputCharacters));
    }
}

/// <summary>Validates Android identifiers before they reach a remote shell.</summary>
public static class AndroidInputValidator
{
    private static readonly System.Text.RegularExpressions.Regex PackageNameRegex = new(
        "^[A-Za-z][A-Za-z0-9_]*(?:\\.[A-Za-z][A-Za-z0-9_]*)+$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant |
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Returns whether a value is a valid Android application id.</summary>
    public static bool IsPackageName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PackageNameRegex.IsMatch(value.Trim());

    /// <summary>Throws when <paramref name="packageName"/> is not an application id.</summary>
    public static string RequirePackageName(string packageName)
    {
        if (!IsPackageName(packageName))
            throw new ArgumentException(
                $"Invalid Android package name '{packageName}'.",
                nameof(packageName));
        return packageName.Trim();
    }

    /// <summary>Throws when a property name contains shell metacharacters.</summary>
    public static string RequirePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)
            || propertyName.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '_' or '-')))
        {
            throw new ArgumentException(
                $"Invalid Android property name '{propertyName}'.",
                nameof(propertyName));
        }

        return propertyName.Trim();
    }

    /// <summary>Quotes one argument for the Android shell.</summary>
    public static string QuoteShellArgument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}
