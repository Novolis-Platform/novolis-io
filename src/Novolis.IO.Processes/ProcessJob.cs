using System.Collections.Concurrent;
using System.Diagnostics;

namespace Novolis.IO.Processes;

/// <summary>A tracked process job.</summary>
public sealed class ProcessJob
{
    /// <summary>Unique id.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display title.</summary>
    public string Title { get; init; } = "";

    /// <summary>Current status.</summary>
    public ProcessJobStatus Status { get; internal set; } = ProcessJobStatus.Queued;

    /// <summary>Detail / last message.</summary>
    public string? Detail { get; internal set; }

    /// <summary>Exit code when finished.</summary>
    public int? ExitCode { get; internal set; }

    internal CancellationTokenSource Cancellation { get; } = new();

    internal Process? Process { get; set; }

    /// <summary>Whether the job can still be cancelled.</summary>
    public bool CanCancel => Status is ProcessJobStatus.Queued or ProcessJobStatus.Running;
}
