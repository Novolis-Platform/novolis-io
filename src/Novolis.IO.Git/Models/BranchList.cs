namespace Novolis.IO.Git;

/// <summary>Local / remote / tag listing.</summary>
public sealed class BranchList
{
    /// <summary>Current branch name (or HEAD).</summary>
    public required string Current { get; init; }

    /// <summary>Local branches.</summary>
    public IReadOnlyList<TipRef> Local { get; init; } = [];

    /// <summary>Remote-tracking branches.</summary>
    public IReadOnlyList<TipRef> Remote { get; init; } = [];

    /// <summary>Tags.</summary>
    public IReadOnlyList<TipRef> Tags { get; init; } = [];
}
