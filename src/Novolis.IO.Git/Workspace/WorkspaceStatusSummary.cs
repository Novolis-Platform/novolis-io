namespace Novolis.IO.Git;

/// <summary>Aggregate counts.</summary>
public sealed class WorkspaceStatusSummary
{
    /// <summary>Total repos listed.</summary>
    public int Total { get; init; }

    /// <summary>Git repos.</summary>
    public int Git { get; init; }

    /// <summary>Dirty.</summary>
    public int Dirty { get; init; }

    /// <summary>Behind.</summary>
    public int Behind { get; init; }

    /// <summary>Ahead.</summary>
    public int Ahead { get; init; }
}
