namespace Novolis.IO.Git;

/// <summary>Multi-select of repos.</summary>
public sealed class RepoSelection
{
    /// <summary>Workspace root.</summary>
    public required string Root { get; init; }

    /// <summary>Selected repos.</summary>
    public IReadOnlyList<RepoEntry> Selected { get; init; } = [];
}
