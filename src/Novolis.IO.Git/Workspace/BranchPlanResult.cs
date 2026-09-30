namespace Novolis.IO.Git;

/// <summary>Apply outcome for a plan.</summary>
public sealed class BranchPlanResult
{
    /// <summary>Plan id.</summary>
    public required string PlanId { get; init; }

    /// <summary>Dry-run flag.</summary>
    public bool DryRun { get; init; }

    /// <summary>Batch-style results.</summary>
    public IReadOnlyList<BatchRepoResult> Results { get; init; } = [];

    /// <summary>Overall ok.</summary>
    public bool Ok => Results.All(r => r.Outcome is "ok" or "skipped");
}
