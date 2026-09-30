namespace Novolis.IO.Git;

/// <summary>A named tip (branch / remote / tag).</summary>
public sealed class TipRef
{
    /// <summary>Ref short name.</summary>
    public required string Name { get; init; }

    /// <summary>Ref kind.</summary>
    public required GitRefKind Kind { get; init; }

    /// <summary>Target commit sha (full or abbreviated).</summary>
    public string? Sha { get; init; }

    /// <summary>Upstream ahead count when known.</summary>
    public int? Ahead { get; init; }

    /// <summary>Upstream behind count when known.</summary>
    public int? Behind { get; init; }
}
