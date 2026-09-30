namespace Novolis.IO.Git;

/// <summary>A branch-cut plan.</summary>
public sealed class BranchPlan
{
    /// <summary>Plan id.</summary>
    public required string Id { get; init; }

    /// <summary>Branch name.</summary>
    public required string Name { get; init; }

    /// <summary>Base ref.</summary>
    public required string BaseRef { get; init; }

    /// <summary>Workspace root.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Steps.</summary>
    public IReadOnlyList<BranchCutRepoStep> Steps { get; init; } = [];

    /// <summary>Created UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
