namespace Novolis.IO.Git;

/// <summary>Aggregate counts for a status probe.</summary>
public sealed class GitStatusSummary
{
    /// <summary>Total repos listed.</summary>
    public int Total { get; init; }

    /// <summary>Repos that returned status.</summary>
    public int Git { get; init; }

    /// <summary>Dirty.</summary>
    public int Dirty { get; init; }

    /// <summary>Behind.</summary>
    public int Behind { get; init; }

    /// <summary>Ahead.</summary>
    public int Ahead { get; init; }
}
