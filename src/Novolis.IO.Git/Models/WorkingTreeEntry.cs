namespace Novolis.IO.Git;

/// <summary>One working-tree path entry.</summary>
public sealed class WorkingTreeEntry
{
    /// <summary>Repository-relative path.</summary>
    public required string Path { get; init; }

    /// <summary>Group.</summary>
    public required WorkingTreeGroup Group { get; init; }

    /// <summary>Raw porcelain XY status code.</summary>
    public string StatusCode { get; init; } = "";
}
