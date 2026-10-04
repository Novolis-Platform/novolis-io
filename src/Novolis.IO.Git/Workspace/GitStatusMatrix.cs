namespace Novolis.IO.Git;

/// <summary>Status snapshot for a rooted set of git checkouts.</summary>
public sealed class GitStatusMatrix
{
    /// <summary>Absolute checkout root.</summary>
    public required string Root { get; init; }

    /// <summary>When the matrix was built (UTC).</summary>
    public DateTimeOffset FetchedAt { get; init; }

    /// <summary>Rows.</summary>
    public IReadOnlyList<RepoStatusRow> Repos { get; init; } = [];

    /// <summary>Summary counts.</summary>
    public GitStatusSummary Summary { get; init; } = new();
}
