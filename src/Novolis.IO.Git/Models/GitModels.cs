namespace Novolis.IO.Git;

/// <summary>Options for checkpoint commits.</summary>
public sealed class CheckpointOptions
{
    /// <summary>When set, only these pathspecs are staged (instead of <c>-A</c>).</summary>
    public IReadOnlyList<string>? Pathspecs { get; init; }

    /// <summary>Whether to push after a successful commit.</summary>
    public bool Push { get; init; } = true;
}
