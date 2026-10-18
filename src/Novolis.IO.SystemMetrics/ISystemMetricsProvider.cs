namespace Novolis.IO.SystemMetrics;

/// <summary>Reads inventory and samples for one capture.</summary>
public interface ISystemMetricsProvider : IAsyncDisposable
{
    /// <summary>Provider name included in diagnostics.</summary>
    string Name { get; }

    /// <summary>Discovers static system resources.</summary>
    ValueTask<SystemMetricsInventory> GetInventoryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Reads one timed system sample.</summary>
    ValueTask<SystemMetricsSample> SampleAsync(
        SystemMetricsSamplingContext context,
        CancellationToken cancellationToken = default);
}
