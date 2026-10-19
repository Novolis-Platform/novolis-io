using System.Globalization;
using System.Text.RegularExpressions;

namespace Novolis.IO.Mobile.Android;

/// <summary>
/// Collects a read-only device, package, launcher, account, and privacy
/// settings inventory through the ADB protocol.
/// </summary>
public sealed class AndroidDeviceInventoryCollector
{
    private static readonly Regex PackageLineRegex = new(
        @"^\s*package:(?<path>.+?)=(?<package>[^\s=]+)(?:\s+versionCode[:=](?<version>\d+))?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PackageNameLineRegex = new(
        @"^\s*package:(?<package>[^\s=]+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VersionCodeRegex = new(
        @"\bversionCode[:=](?<version>\d+)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LauncherComponentRegex = new(
        @"^\s*(?<package>[A-Za-z0-9_.$-]+)/(?<activity>\S+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PriorityRegex = new(
        @"\bpriority=(?<priority>-?\d+)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DefaultRegex = new(
        @"\bisDefault=(?<default>true|false)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AccountCountRegex = new(
        @"(?m)^\s*Accounts:\s*(?<count>\d+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AccountTypeRegex = new(
        @"\bAccount\s*\{[^}\r\n]*\btype=(?<type>[^,}\s]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AuthenticatorTypeRegex = new(
        @"\bAuthenticatorDescription\s*\{type=(?<type>[^,}\s]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly (string Scope, string Name)[] SettingKeys =
    [
        ("global", "device_provisioned"),
        ("global", "captive_portal_mode"),
        ("global", "network_recommendations_enabled"),
        ("global", "ble_scan_always_enabled"),
        ("global", "wifi_scan_always_enabled"),
        ("global", "private_dns_mode"),
        ("global", "private_dns_specifier"),
        ("global", "http_proxy"),
        ("global", "always_on_vpn_app"),
        ("secure", "user_setup_complete"),
        ("secure", "backup_enabled"),
        ("secure", "install_non_market_apps"),
    ];

    private readonly AndroidDebugBridge _adb;

    /// <summary>Creates an inventory collector bound to an ADB bridge.</summary>
    public AndroidDeviceInventoryCollector(AndroidDebugBridge adb)
    {
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));
    }

    /// <summary>ADB bridge used for collection.</summary>
    public AndroidDebugBridge Adb => _adb;

    /// <summary>Collects an inventory synchronously.</summary>
    public AndroidInventoryResult Collect(string? serial = null) =>
        CollectAsync(serial).GetAwaiter().GetResult();

    /// <summary>Collects an inventory without changing device state.</summary>
    public async Task<AndroidInventoryResult> CollectAsync(
        string? serial = null,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        try
        {
            var device = await _adb.GetDeviceInfoAsync(serial, cancellationToken)
                .ConfigureAwait(false);

            var packageOutput = await ReadShellAsync(
                    "pm list packages -f --show-versioncode",
                    "package inventory",
                    serial,
                    warnings,
                    cancellationToken)
                .ConfigureAwait(false);
            var systemPackageOutput = await ReadShellAsync(
                    "pm list packages -s",
                    "system package classification",
                    serial,
                    warnings,
                    cancellationToken)
                .ConfigureAwait(false);
            var disabledPackageOutput = await ReadShellAsync(
                    "pm list packages -d",
                    "disabled package classification",
                    serial,
                    warnings,
                    cancellationToken)
                .ConfigureAwait(false);
            var launcherOutput = await ReadShellAsync(
                    "cmd package query-activities --brief -a android.intent.action.MAIN -c android.intent.category.LAUNCHER",
                    "launcher activity inventory",
                    serial,
                    warnings,
                    cancellationToken)
                .ConfigureAwait(false);
            var accountOutput = await ReadShellAsync(
                    "dumpsys account",
                    "account inventory",
                    serial,
                    warnings,
                    cancellationToken)
                .ConfigureAwait(false);

            var settingValues = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (scope, name) in SettingKeys)
            {
                var output = await ReadShellAsync(
                        $"settings get {scope} {name}",
                        $"setting {scope}.{name}",
                        serial,
                        warnings,
                        cancellationToken)
                    .ConfigureAwait(false);
                settingValues[$"{scope}.{name}"] = NormalizeSettingValue(output);
            }

            var inventory = new AndroidDeviceInventory
            {
                CollectedAtUtc = DateTimeOffset.UtcNow,
                Device = device,
                Packages = ParsePackages(
                    packageOutput,
                    systemPackageOutput,
                    disabledPackageOutput),
                LauncherActivities = ParseLauncherActivities(launcherOutput),
                Accounts = ParseAccounts(accountOutput),
                Settings = ParseSettings(settingValues),
            };

            return new AndroidInventoryResult
            {
                Ok = true,
                Inventory = inventory,
                Warnings = warnings,
            };
        }
        catch (AndroidOperationException ex)
        {
            return new AndroidInventoryResult
            {
                Ok = false,
                Warnings = warnings,
                Failure = ex.Failure,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new AndroidInventoryResult
            {
                Ok = false,
                Warnings = warnings,
                Failure = new AndroidFailure(
                    AndroidFailureKind.Cancelled,
                    "Android inventory collection was cancelled.",
                    serial),
            };
        }
        catch (Exception ex)
        {
            return new AndroidInventoryResult
            {
                Ok = false,
                Warnings = warnings,
                Failure = new AndroidFailure(
                    AndroidFailureKind.Unknown,
                    ex.Message,
                    serial,
                    Exception: ex),
            };
        }
    }

    /// <summary>Parses versioned package rows and package state lists.</summary>
    public static IReadOnlyList<AndroidInstalledPackage> ParsePackages(
        string? packageOutput,
        string? systemPackageOutput,
        string? disabledPackageOutput)
    {
        var systemPackages = ParsePackageNames(systemPackageOutput);
        var disabledPackages = ParsePackageNames(disabledPackageOutput);
        var packages = new Dictionary<string, AndroidInstalledPackage>(
            StringComparer.Ordinal);

        foreach (var line in SplitLines(packageOutput))
        {
            var match = PackageLineRegex.Match(line);
            if (!match.Success)
                continue;

            var packageName = match.Groups["package"].Value;
            var path = match.Groups["path"].Value.Trim();
            var version = ParseLong(match.Groups["version"].Value);
            if (version is null)
            {
                var versionMatch = VersionCodeRegex.Match(line);
                version = versionMatch.Success
                    ? ParseLong(versionMatch.Groups["version"].Value)
                    : null;
            }

            packages[packageName] = new AndroidInstalledPackage
            {
                PackageName = packageName,
                ApkPath = path,
                VersionCode = version,
                IsSystem = systemPackages.Contains(packageName)
                    || IsSystemPath(path),
                IsEnabled = !disabledPackages.Contains(packageName),
            };
        }

        return packages.Values
            .OrderBy(package => package.PackageName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Parses PackageManager launcher activity output.</summary>
    public static IReadOnlyList<AndroidLauncherActivity> ParseLauncherActivities(
        string? output)
    {
        var activities = new List<AndroidLauncherActivity>();
        var priority = 0;
        var isDefault = false;

        foreach (var line in SplitLines(output))
        {
            var priorityMatch = PriorityRegex.Match(line);
            if (priorityMatch.Success)
                priority = ParseInt(priorityMatch.Groups["priority"].Value);

            var defaultMatch = DefaultRegex.Match(line);
            if (defaultMatch.Success)
                isDefault = string.Equals(
                    defaultMatch.Groups["default"].Value,
                    "true",
                    StringComparison.OrdinalIgnoreCase);

            var componentMatch = LauncherComponentRegex.Match(line);
            if (!componentMatch.Success)
                continue;

            activities.Add(new AndroidLauncherActivity
            {
                PackageName = componentMatch.Groups["package"].Value,
                ActivityName = componentMatch.Groups["activity"].Value,
                Priority = priority,
                IsDefault = isDefault,
            });
            priority = 0;
            isDefault = false;
        }

        return activities
            .DistinctBy(
                activity => $"{activity.PackageName}/{activity.ActivityName}",
                StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Parses account counts and account types without retaining names.</summary>
    public static AndroidAccountSnapshot ParseAccounts(string? output)
    {
        var text = output ?? string.Empty;
        var countMatch = AccountCountRegex.Match(text);
        var accountTypes = AccountTypeRegex
            .Matches(text)
            .Select(match => match.Groups["type"].Value)
            .Where(type => type.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();
        var authenticatorTypes = AuthenticatorTypeRegex
            .Matches(text)
            .Select(match => match.Groups["type"].Value)
            .Where(type => type.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();

        var accountCount = countMatch.Success
            ? ParseInt(countMatch.Groups["count"].Value)
            : AccountTypeRegex.Matches(text).Count;

        return new AndroidAccountSnapshot
        {
            AccountCount = accountCount,
            AccountTypes = accountTypes,
            AuthenticatorTypes = authenticatorTypes,
        };
    }

    /// <summary>Creates a settings snapshot from normalized key/value pairs.</summary>
    public static AndroidSettingsSnapshot ParseSettings(
        IEnumerable<KeyValuePair<string, string?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new AndroidSettingsSnapshot
        {
            Values = values
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(
                    pair => pair.Key,
                    pair => NormalizeSettingValue(pair.Value),
                    StringComparer.Ordinal),
        };
    }

    private async Task<string?> ReadShellAsync(
        string command,
        string label,
        string? serial,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var result = await _adb.ShellAsync(command, serial, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
        {
            warnings.Add(
                $"{label}: {(
                    string.IsNullOrWhiteSpace(result.Diagnostic)
                        ? "command failed"
                        : result.Diagnostic).Trim()}");
            return null;
        }

        return result.StdOut;
    }

    private static HashSet<string> ParsePackageNames(string? output)
    {
        var packages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in SplitLines(output))
        {
            var match = PackageNameLineRegex.Match(line);
            if (match.Success)
                packages.Add(match.Groups["package"].Value);
        }

        return packages;
    }

    private static bool IsSystemPath(string path) =>
        path.StartsWith("/system/", StringComparison.Ordinal)
        || path.StartsWith("/vendor/", StringComparison.Ordinal)
        || path.StartsWith("/product/", StringComparison.Ordinal)
        || path.StartsWith("/odm/", StringComparison.Ordinal);

    private static IEnumerable<string> SplitLines(string? value) =>
        (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static long? ParseLong(string value) =>
        long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static int ParseInt(string value) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;

    private static string? NormalizeSettingValue(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }
}
