namespace Novolis.IO.Git;

/// <summary>Options for pull.</summary>
public sealed class PullOptions
{
    /// <summary>Remote name.</summary>
    public string Remote { get; init; } = "origin";

    /// <summary>When true (default), uses --ff-only.</summary>
    public bool FfOnly { get; init; } = true;
}
