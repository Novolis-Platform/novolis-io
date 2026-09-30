namespace Novolis.IO.Git;

/// <summary>One stash entry.</summary>
public sealed class StashEntry
{
    /// <summary>stash@{n} index.</summary>
    public required int Index { get; init; }

    /// <summary>Message.</summary>
    public required string Message { get; init; }

    /// <summary>Optional timestamp.</summary>
    public string? At { get; init; }

    /// <summary>Optional stash commit sha.</summary>
    public string? Sha { get; init; }
}
