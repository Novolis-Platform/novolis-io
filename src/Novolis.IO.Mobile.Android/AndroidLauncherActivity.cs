namespace Novolis.IO.Mobile.Android;

/// <summary>One activity returned for the Android launcher intent.</summary>
public sealed record AndroidLauncherActivity
{
    /// <summary>Android application id owning the activity.</summary>
    public required string PackageName { get; init; }

    /// <summary>Activity component name as reported by PackageManager.</summary>
    public required string ActivityName { get; init; }

    /// <summary>PackageManager intent priority.</summary>
    public int Priority { get; init; }

    /// <summary>Whether PackageManager marks this activity as a default match.</summary>
    public bool IsDefault { get; init; }
}
