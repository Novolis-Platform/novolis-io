namespace Novolis.IO.Git;

/// <summary>A diff hunk.</summary>
public sealed class DiffHunk
{
    /// <summary>Header (@@ … @@).</summary>
    public required string Header { get; init; }

    /// <summary>Lines.</summary>
    public IReadOnlyList<DiffLine> Lines { get; init; } = [];
}
