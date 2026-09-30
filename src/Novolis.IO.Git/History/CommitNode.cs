namespace Novolis.IO.Git;

/// <summary>One commit node with lane geometry.</summary>
public sealed class CommitNode
{
    /// <summary>Full sha.</summary>
    public required string Sha { get; init; }

    /// <summary>Short sha.</summary>
    public required string ShortSha { get; init; }

    /// <summary>Subject.</summary>
    public required string Subject { get; init; }

    /// <summary>Author name.</summary>
    public string? AuthorName { get; init; }

    /// <summary>Author date.</summary>
    public string? AuthorAt { get; init; }

    /// <summary>Parent shas.</summary>
    public IReadOnlyList<string> Parents { get; init; } = [];

    /// <summary>Lane index (0-based).</summary>
    public int Lane { get; init; }

    /// <summary>Row index (0 = newest).</summary>
    public int Row { get; init; }

    /// <summary>Logical X.</summary>
    public double X { get; init; }

    /// <summary>Logical Y.</summary>
    public double Y { get; init; }

    /// <summary>Whether this is a merge (2+ parents).</summary>
    public bool IsMerge { get; init; }
}
