namespace Novolis.IO.Mobile.Android;

/// <summary>One installed Android package from the selected user.</summary>
public sealed record AndroidInstalledPackage
{
    /// <summary>Android application id.</summary>
    public required string PackageName { get; init; }

    /// <summary>APK path reported by <c>pm</c>, when available.</summary>
    public string? ApkPath { get; init; }

    /// <summary>Installed version code, when reported by <c>pm</c>.</summary>
    public long? VersionCode { get; init; }

    /// <summary>Whether the package is part of the system image.</summary>
    public bool IsSystem { get; init; }

    /// <summary>Whether the package is enabled for the selected user.</summary>
    public bool IsEnabled { get; init; }
}
