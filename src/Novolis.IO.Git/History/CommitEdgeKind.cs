namespace Novolis.IO.Git;

/// <summary>Kind of graph edge.</summary>
public enum CommitEdgeKind
{
    /// <summary>First/parent edge.</summary>
    Parent,

    /// <summary>Additional merge parent.</summary>
    Merge,
}
