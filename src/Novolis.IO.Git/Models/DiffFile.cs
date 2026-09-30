namespace Novolis.IO.Git;

/// <summary>Diff for one path.</summary>
public sealed class DiffFile
{
    /// <summary>Path (new side when renamed).</summary>
    public required string Path { get; init; }

    /// <summary>Old path when renamed.</summary>
    public string? OldPath { get; init; }

    /// <summary>Hunks.</summary>
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

    /// <summary>Whether binary.</summary>
    public bool IsBinary { get; init; }
}
