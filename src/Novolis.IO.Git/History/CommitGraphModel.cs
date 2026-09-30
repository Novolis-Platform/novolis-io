namespace Novolis.IO.Git;

/// <summary>Full graph model for UI / JSON.</summary>
public sealed class CommitGraphModel
{
    /// <summary>Nodes newest-first.</summary>
    public IReadOnlyList<CommitNode> Nodes { get; init; } = [];

    /// <summary>Edges.</summary>
    public IReadOnlyList<CommitEdge> Edges { get; init; } = [];

    /// <summary>Lanes.</summary>
    public IReadOnlyList<CommitLane> Lanes { get; init; } = [];

    /// <summary>Tip refs anchored on nodes.</summary>
    public IReadOnlyList<TipRef> TipRefs { get; init; } = [];
}
