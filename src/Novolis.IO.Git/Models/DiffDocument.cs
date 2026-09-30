namespace Novolis.IO.Git;

/// <summary>Parsed unified diff document.</summary>
public sealed class DiffDocument
{
    /// <summary>Files.</summary>
    public IReadOnlyList<DiffFile> Files { get; init; } = [];
}
