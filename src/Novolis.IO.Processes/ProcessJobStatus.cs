using System.Collections.Concurrent;
using System.Diagnostics;

namespace Novolis.IO.Processes;

/// <summary>Status of a queued/running job.</summary>
public enum ProcessJobStatus
{
    /// <summary>Waiting to start.</summary>
    Queued,
    /// <summary>Currently running.</summary>
    Running,
    /// <summary>Finished successfully.</summary>
    Succeeded,
    /// <summary>Finished with failure.</summary>
    Failed,
    /// <summary>Cancelled.</summary>
    Cancelled
}
