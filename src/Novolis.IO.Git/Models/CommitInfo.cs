namespace Novolis.IO.Git;

/// <summary>Log / detail commit summary.</summary>
public sealed class CommitInfo
{
    /// <summary>Full sha.</summary>
    public required string Sha { get; init; }

    /// <summary>Short sha.</summary>
    public required string ShortSha { get; init; }

    /// <summary>Subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Body (optional).</summary>
    public string? Body { get; init; }

    /// <summary>Author name.</summary>
    public string? AuthorName { get; init; }

    /// <summary>Author email.</summary>
    public string? AuthorEmail { get; init; }

    /// <summary>Author date ISO.</summary>
    public string? AuthorAt { get; init; }

    /// <summary>Parent shas.</summary>
    public IReadOnlyList<string> Parents { get; init; } = [];
}
