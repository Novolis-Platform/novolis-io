namespace Novolis.IO.GitHub;

/// <summary>Address of a public GitHub repository and its stable web URLs.</summary>
public sealed record GitHubRepository
{
    /// <summary>GitHub owner or organization.</summary>
    public required string Owner { get; init; }

    /// <summary>GitHub repository name.</summary>
    public required string Name { get; init; }

    /// <summary>Creates the raw-content URL for a repository file.</summary>
    public Uri RawFile(string branch, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var encodedPath = string.Join(
            "/",
            path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        return new Uri($"https://raw.githubusercontent.com/{Owner}/{Name}/{branch.Trim('/')}/{encodedPath}");
    }

    /// <summary>Creates the web URL for the repository's latest release.</summary>
    public Uri LatestReleasePage() =>
        new($"https://github.com/{Owner}/{Name}/releases/latest");
}
