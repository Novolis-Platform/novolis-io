namespace Novolis.IO.Git;

/// <summary>Planned branch-cut for one repo.</summary>
public sealed class BranchCutRepoStep
{
    /// <summary>Repo.</summary>
    public required GitRepositoryWorkspace Repo { get; init; }

    /// <summary>Planned git argv.</summary>
    public required IReadOnlyList<string> PlannedArgs { get; init; }

    /// <summary>Block reason if not applicable.</summary>
    public string? BlockReason { get; init; }
}
