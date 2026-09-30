namespace Novolis.IO.Git;

/// <summary>Grouped working tree status.</summary>
public sealed class WorkingTreeStatus
{
    /// <summary>Staged paths.</summary>
    public IReadOnlyList<WorkingTreeEntry> Staged { get; init; } = [];

    /// <summary>Unstaged paths.</summary>
    public IReadOnlyList<WorkingTreeEntry> Unstaged { get; init; } = [];

    /// <summary>Untracked paths.</summary>
    public IReadOnlyList<WorkingTreeEntry> Untracked { get; init; } = [];
}
