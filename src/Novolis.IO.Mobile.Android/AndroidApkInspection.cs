namespace Novolis.IO.Mobile.Android;

/// <summary>Metadata extracted from an Android APK using Android SDK tools.</summary>
public sealed record AndroidApkInspection
{
    /// <summary>Whether package metadata was read successfully.</summary>
    public bool Ok { get; init; }

    /// <summary>Resolved APK path.</summary>
    public required string ApkPath { get; init; }

    /// <summary>Package name from AndroidManifest.xml.</summary>
    public string? PackageName { get; init; }

    /// <summary>Version name from AndroidManifest.xml.</summary>
    public string? VersionName { get; init; }

    /// <summary>Version code from AndroidManifest.xml.</summary>
    public long? VersionCode { get; init; }

    /// <summary>Native ABIs declared by the APK, when present.</summary>
    public IReadOnlyList<string> NativeAbis { get; init; } = [];

    /// <summary>SHA-256 digest of the APK signing certificate, when available.</summary>
    public string? SigningCertificateSha256 { get; init; }

    /// <summary>Android SDK tool paths used for inspection.</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>Blocking inspection errors.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Non-blocking inspection warnings.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
