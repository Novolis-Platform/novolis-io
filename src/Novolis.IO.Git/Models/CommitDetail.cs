namespace Novolis.IO.Git;

/// <summary>Commit detail with file change summary.</summary>
public sealed class CommitDetail
{
    /// <summary>Commit info.</summary>
    public required CommitInfo Commit { get; init; }

    /// <summary>Files added.</summary>
    public int FilesAdded { get; init; }

    /// <summary>Files deleted.</summary>
    public int FilesDeleted { get; init; }

    /// <summary>Files modified.</summary>
    public int FilesModified { get; init; }

    /// <summary>Changed paths.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];
}
