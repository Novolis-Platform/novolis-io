namespace Novolis.IO.Git;

/// <summary>Options for commit log / graph fetch.</summary>
public sealed class CommitLogOptions
{
    /// <summary>Max commits (default 200).</summary>
    public int MaxCount { get; init; } = 200;

    /// <summary>When true, only first parent.</summary>
    public bool FirstParent { get; init; }
}
