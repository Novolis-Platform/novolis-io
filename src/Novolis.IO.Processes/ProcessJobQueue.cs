using System.Collections.Concurrent;
using System.Diagnostics;

namespace Novolis.IO.Processes;

/// <summary>Specification for an external process job.</summary>
public sealed class ProcessJobSpec
{
    /// <summary>Executable path or name.</summary>
    public required string FileName { get; init; }

    /// <summary>Arguments.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Working directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Display title.</summary>
    public string? Title { get; init; }
}

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

/// <summary>Kills a process and its children.</summary>
public static class ProcessTree
{
    /// <summary>Kills the process tree for <paramref name="pid"/> (Windows: taskkill /T /F).</summary>
    public static void Kill(int pid)
    {
        if (pid <= 0)
            return;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pid} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                killer?.WaitForExit(5000);
            }
            else
            {
                try
                {
                    Process.GetProcessById(pid).Kill(entireProcessTree: true);
                }
                catch
                {
                    /* ignore */
                }
            }
        }
        catch
        {
            /* ignore */
        }
    }
}

/// <summary>Runs a bounded number of process jobs concurrently.</summary>
public sealed class ProcessJobQueue
{
    readonly object _gate = new();
    readonly Queue<(ProcessJob Job, ProcessJobSpec Spec)> _pending = new();
    readonly HashSet<Guid> _running = [];
    int _maxParallel = 2;
    bool _pumpScheduled;

    /// <summary>Jobs (newest first).</summary>
    public ConcurrentQueue<ProcessJob> Jobs { get; } = new();

    /// <summary>Maximum concurrent processes (1–32).</summary>
    public int MaxParallel
    {
        get { lock (_gate) return _maxParallel; }
        set
        {
            lock (_gate)
                _maxParallel = Math.Clamp(value, 1, 32);
            SchedulePump();
        }
    }

    /// <summary>Raised when queue state changes.</summary>
    public event Action? Changed;

    /// <summary>Enqueues a job.</summary>
    public ProcessJob Enqueue(ProcessJobSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var job = new ProcessJob { Title = string.IsNullOrWhiteSpace(spec.Title) ? spec.FileName : spec.Title! };
        Jobs.Enqueue(job);
        lock (_gate)
            _pending.Enqueue((job, spec));
        SchedulePump();
        Changed?.Invoke();
        return job;
    }

    /// <summary>Cancels a job and kills its process tree if running.</summary>
    public void Cancel(ProcessJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.CanCancel)
            return;
        try { job.Cancellation.Cancel(); } catch { /* ignore */ }

        lock (_gate)
        {
            var kept = new Queue<(ProcessJob, ProcessJobSpec)>();
            while (_pending.Count > 0)
            {
                var item = _pending.Dequeue();
                if (item.Job.Id == job.Id)
                {
                    job.Status = ProcessJobStatus.Cancelled;
                    job.Detail = "Cancelled before start.";
                    continue;
                }
                kept.Enqueue(item);
            }
            while (kept.Count > 0)
                _pending.Enqueue(kept.Dequeue());
        }

        if (job.Process is { HasExited: false } proc)
            ProcessTree.Kill(proc.Id);

        Changed?.Invoke();
        SchedulePump();
    }

    void SchedulePump()
    {
        lock (_gate)
        {
            if (_pumpScheduled)
                return;
            _pumpScheduled = true;
        }

        _ = Task.Run(PumpAsync);
    }

    async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                (ProcessJob Job, ProcessJobSpec Spec)? next = null;
                lock (_gate)
                {
                    if (_pending.Count == 0 || _running.Count >= _maxParallel)
                    {
                        _pumpScheduled = false;
                        return;
                    }

                    next = _pending.Dequeue();
                    _running.Add(next.Value.Job.Id);
                }

                Changed?.Invoke();
                _ = RunOneAsync(next.Value.Job, next.Value.Spec);
            }
        }
        catch
        {
            lock (_gate)
                _pumpScheduled = false;
            throw;
        }
    }

    async Task RunOneAsync(ProcessJob job, ProcessJobSpec spec)
    {
        var ct = job.Cancellation.Token;
        job.Status = ProcessJobStatus.Running;
        job.Detail = "Starting...";
        Changed?.Invoke();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = spec.FileName,
                WorkingDirectory = spec.WorkingDirectory ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in spec.Arguments)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start process.");
            job.Process = process;
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            job.ExitCode = process.ExitCode;
            job.Status = process.ExitCode == 0 ? ProcessJobStatus.Succeeded : ProcessJobStatus.Failed;
            job.Detail = process.ExitCode == 0 ? "Succeeded." : $"Exit code {process.ExitCode}.";
        }
        catch (OperationCanceledException)
        {
            if (job.Process is { HasExited: false } p)
                ProcessTree.Kill(p.Id);
            job.Status = ProcessJobStatus.Cancelled;
            job.Detail = "Cancelled.";
        }
        catch (Exception ex)
        {
            job.Status = ProcessJobStatus.Failed;
            job.Detail = ex.Message;
        }
        finally
        {
            lock (_gate)
                _running.Remove(job.Id);
            Changed?.Invoke();
            SchedulePump();
        }
    }
}
