namespace Novolis.IO.Platform.Android;

/// <summary>Resolved directories for one Android product.</summary>
public sealed class AndroidAppLocations
{
    /// <summary>Creates a resolved location set.</summary>
    public AndroidAppLocations(
        string productName,
        AndroidStorageKind kind,
        string rootDirectory,
        string workspaceDirectory,
        string cacheDirectory,
        string? applicationId = null)
    {
        ProductName = AndroidStorageLayout.SanitizeProductName(productName);
        Kind = kind;
        RootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        WorkspaceDirectory = workspaceDirectory ?? throw new ArgumentNullException(nameof(workspaceDirectory));
        CacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        ApplicationId = string.IsNullOrWhiteSpace(applicationId) ? null : applicationId.Trim();
    }

    /// <summary>Sanitized product folder name.</summary>
    public string ProductName { get; }

    /// <summary>Which Android location <see cref="RootDirectory"/> uses.</summary>
    public AndroidStorageKind Kind { get; }

    /// <summary>Writable product root. Created before this object is returned.</summary>
    public string RootDirectory { get; }

    /// <summary><c>{RootDirectory}/workspace</c>.</summary>
    public string WorkspaceDirectory { get; }

    /// <summary>Cache directory for this product. Android may clear this.</summary>
    public string CacheDirectory { get; }

    /// <summary>Android package name when resolved on device.</summary>
    public string? ApplicationId { get; }
}
