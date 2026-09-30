namespace Novolis.IO.Git;

/// <summary>Workspace status matrix.</summary>
public sealed class WorkspaceStatusMatrix
{
    /// <summary>Absolute workspace root.</summary>
    public required string Root { get; init; }

    /// <summary>When matrix was built (UTC).</summary>
    public DateTimeOffset FetchedAt { get; init; }

    /// <summary>Rows.</summary>
    public IReadOnlyList<RepoStatusRow> Repos { get; init; } = [];

    /// <summary>Summary counts.</summary>
    public WorkspaceStatusSummary Summary { get; init; } = new();
}
