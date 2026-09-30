namespace Novolis.IO.Git;

/// <summary>Edge between commits.</summary>
public sealed class CommitEdge
{
    /// <summary>Child (newer) sha.</summary>
    public required string From { get; init; }

    /// <summary>Parent (older) sha.</summary>
    public required string To { get; init; }

    /// <summary>Edge kind.</summary>
    public required CommitEdgeKind Kind { get; init; }

    /// <summary>From lane.</summary>
    public int FromLane { get; init; }

    /// <summary>To lane.</summary>
    public int ToLane { get; init; }
}
