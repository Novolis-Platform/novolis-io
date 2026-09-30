namespace Novolis.IO.Git;

/// <summary>One discovered repository under a workspace root.</summary>
public sealed class RepoEntry
{
    /// <summary>Folder name (e.g. novolis-io).</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path.</summary>
    public required string Path { get; init; }

    /// <summary>Whether a .git directory/file exists.</summary>
    public bool IsGit { get; init; }
}
