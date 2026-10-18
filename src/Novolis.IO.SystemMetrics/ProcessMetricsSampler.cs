using System.Diagnostics;

namespace Novolis.IO.SystemMetrics;

internal sealed class ProcessMetricsSampler
{
    private TimeSpan? _previousProcessorTime;
    private TimeSpan? _previousElapsed;

    public ProcessMetrics? Read(int? processId, TimeSpan elapsed)
    {
        if (processId is not { } pid)
            return null;

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Refresh();
            var processorTime = process.TotalProcessorTime;
            var cpu = CalculateCpu(processorTime, elapsed);
            _previousProcessorTime = processorTime;
            _previousElapsed = elapsed;
            return new ProcessMetrics(
                pid,
                TryRead(() => process.ProcessName),
                cpu,
                processorTime,
                TryRead(() => process.WorkingSet64),
                TryRead(() => process.PrivateMemorySize64),
                TryRead(() => process.Threads.Count),
                null,
                null);
        }
        catch (Exception ex) when (
            ex is ArgumentException
                or InvalidOperationException
                or NotSupportedException
                or System.ComponentModel.Win32Exception)
        {
            _previousProcessorTime = null;
            _previousElapsed = null;
            return new ProcessMetrics(
                pid,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                SystemMetricStatus.Unavailable);
        }
    }

    private double? CalculateCpu(
        TimeSpan processorTime,
        TimeSpan elapsed)
    {
        // A first sample has no delta and is intentionally reported as null.
        if (_previousProcessorTime is not { } previousProcessorTime
            || _previousElapsed is not { } previousElapsed)
        {
            return null;
        }

        var wallSeconds = (elapsed - previousElapsed).TotalSeconds;
        if (wallSeconds <= 0 || processorTime < previousProcessorTime)
            return null;

        return Math.Clamp(
            (processorTime - previousProcessorTime).TotalSeconds
                / wallSeconds
                / Math.Max(1, Environment.ProcessorCount)
                * 100,
            0,
            100);
    }

    private static T? TryRead<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return default;
        }
    }
}
