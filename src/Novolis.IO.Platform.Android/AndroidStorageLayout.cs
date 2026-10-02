namespace Novolis.IO.Platform.Android;

/// <summary>Folder names for Novolis Android app data.</summary>
public static class AndroidStorageLayout
{
    /// <summary>Organization folder under Documents, app-specific files, and cache.</summary>
    public const string OrganizationFolder = "Novolis";

    /// <summary>Workspace directory name under the product root.</summary>
    public const string WorkspaceFolder = "workspace";

    /// <summary>Returns <paramref name="productName"/> when it is a single folder name.</summary>
    public static string SanitizeProductName(string productName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        var trimmed = productName.Trim();
        if (trimmed is "." or ".."
            || trimmed.Contains('/', StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal)
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "Product name must be a single folder name.",
                nameof(productName));
        }

        return trimmed;
    }

    /// <summary><c>{parent}/Novolis/{product}</c>.</summary>
    public static string UnderOrganization(string parentDirectory, string productName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        return Path.Combine(parentDirectory, OrganizationFolder, SanitizeProductName(productName));
    }

    /// <summary><c>{root}/workspace</c>.</summary>
    public static string Workspace(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.Combine(rootDirectory, WorkspaceFolder);
    }

    /// <summary>MediaStore relative path <c>Documents/Novolis/{product}</c>.</summary>
    public static string MediaStoreDocumentsRelative(string productName) =>
        $"Documents/{OrganizationFolder}/{SanitizeProductName(productName)}";

    /// <summary>Shell command that lists or pulls <paramref name="locations"/>.</summary>
    public static string DescribeForAdb(AndroidAppLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        if (locations.Kind == AndroidStorageKind.SharedDocuments)
            return $"adb pull \"{locations.RootDirectory}\"";

        if (!string.IsNullOrWhiteSpace(locations.ApplicationId))
            return $"adb shell run-as {locations.ApplicationId} ls \"{locations.RootDirectory}\"";

        return locations.RootDirectory;
    }
}
