namespace Novolis.IO.Git;

/// <summary>Status row for one repo.</summary>
public sealed class RepoStatusRow
{
    /// <summary>Repo identity.</summary>
    public required GitRepositoryWorkspace Repo { get; init; }

    /// <summary>Status when git; null when not.</summary>
    public GitStatus? Status { get; init; }

    /// <summary>Stash count when known.</summary>
    public int StashCount { get; init; }

    /// <summary>Last fetch UTC (from state store).</summary>
    public DateTimeOffset? LastFetchAt { get; init; }

    /// <summary>Error reading status.</summary>
    public string? Error { get; init; }
}
