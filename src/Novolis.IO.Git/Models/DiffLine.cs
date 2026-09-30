namespace Novolis.IO.Git;

/// <summary>One unified-diff hunk line.</summary>
public sealed class DiffLine
{
    /// <summary>Prefix: space, +, or -.</summary>
    public required char Kind { get; init; }

    /// <summary>Line text without prefix.</summary>
    public required string Text { get; init; }
}
