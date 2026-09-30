namespace Novolis.IO.Git;

/// <summary>Per-repo batch outcome.</summary>
public sealed class BatchRepoResult
{
    /// <summary>Repo.</summary>
    public required RepoEntry Repo { get; init; }

    /// <summary>ok | skipped | failed.</summary>
    public required string Outcome { get; init; }

    /// <summary>Message.</summary>
    public required string Message { get; init; }

    /// <summary>Optional planned argv for dry-run.</summary>
    public IReadOnlyList<string>? PlannedArgs { get; init; }
}
