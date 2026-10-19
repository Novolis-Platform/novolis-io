namespace Novolis.IO.Mobile.Android;

/// <summary>Read-only compatibility and provisioning inventory for one device.</summary>
public sealed record AndroidDeviceInventory
{
    /// <summary>UTC time at which the inventory was collected.</summary>
    public required DateTimeOffset CollectedAtUtc { get; init; }

    /// <summary>Technical device snapshot.</summary>
    public required AndroidDeviceInfo Device { get; init; }

    /// <summary>Installed packages visible to the selected user.</summary>
    public IReadOnlyList<AndroidInstalledPackage> Packages { get; init; } = [];

    /// <summary>Activities that answer the standard Android launcher intent.</summary>
    public IReadOnlyList<AndroidLauncherActivity> LauncherActivities { get; init; } = [];

    /// <summary>Privacy-safe account summary.</summary>
    public AndroidAccountSnapshot Accounts { get; init; } = new();

    /// <summary>Selected provisioning and privacy settings.</summary>
    public AndroidSettingsSnapshot Settings { get; init; } = new();
}
