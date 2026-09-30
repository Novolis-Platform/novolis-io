namespace Novolis.IO.Git;

/// <summary>Options for commit graph layout.</summary>
public sealed class CommitGraphOptions
{
    /// <summary>Max commits.</summary>
    public int MaxCount { get; init; } = 200;

    /// <summary>First-parent only.</summary>
    public bool FirstParent { get; init; }

    /// <summary>Logical row height units.</summary>
    public double RowHeight { get; init; } = 1;

    /// <summary>Logical lane width units.</summary>
    public double LaneWidth { get; init; } = 1;
}
