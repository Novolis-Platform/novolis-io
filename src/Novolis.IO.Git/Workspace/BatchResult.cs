namespace Novolis.IO.Git;

/// <summary>Batch operation result.</summary>
public sealed class BatchResult
{
    /// <summary>Per-repo results.</summary>
    public IReadOnlyList<BatchRepoResult> Results { get; init; } = [];

    /// <summary>True when every non-skipped repo succeeded.</summary>
    public bool Ok => Results.All(r => r.Outcome is "ok" or "skipped");

    /// <summary>Any failures.</summary>
    public bool HasFailures => Results.Any(r => r.Outcome == "failed");
}
