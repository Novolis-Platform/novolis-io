namespace Novolis.IO.Git;

/// <summary>Batch options.</summary>
public sealed class BatchOptions
{
    /// <summary>Max parallel git processes.</summary>
    public int Parallel { get; init; } = 6;

    /// <summary>When true, skip dirty repos on mutate.</summary>
    public bool SkipDirty { get; init; } = true;

    /// <summary>Dry-run (no mutate).</summary>
    public bool DryRun { get; init; }

    /// <summary>Workspace root for locks/state.</summary>
    public string? WorkspaceRoot { get; init; }
}
