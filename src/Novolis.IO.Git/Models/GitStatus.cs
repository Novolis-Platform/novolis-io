namespace Novolis.IO.Git;

/// <summary>Compact git status snapshot.</summary>
public sealed class GitStatus
{
    /// <summary>Current branch name.</summary>
    public required string Branch { get; init; }

    /// <summary>Upstream branch, if any.</summary>
    public string? Upstream { get; init; }

    /// <summary>Commits ahead of upstream.</summary>
    public int Ahead { get; init; }

    /// <summary>Commits behind upstream.</summary>
    public int Behind { get; init; }

    /// <summary>Whether the work tree is dirty.</summary>
    public bool Dirty { get; init; }

    /// <summary>Porcelain dirty file lines.</summary>
    public IReadOnlyList<string> DirtyFiles { get; init; } = [];

    /// <summary>Active pass id from the pass store, if any.</summary>
    public string? ActivePass { get; init; }

    /// <summary>Last commit ISO timestamp.</summary>
    public string? LastCommitAt { get; init; }

    /// <summary>Last commit short sha.</summary>
    public string? LastCommitSha { get; init; }

    /// <summary>Last commit subject.</summary>
    public string? LastCommitMessage { get; init; }
}
