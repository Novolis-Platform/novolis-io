namespace Novolis.IO.SystemMetrics;

/// <summary>Timing and workload context supplied to a provider sample.</summary>
public sealed record SystemMetricsSamplingContext(
    DateTimeOffset Timestamp,
    TimeSpan Elapsed,
    int? ProcessId,
    string? SelectedGpuAdapterId,
    SystemMetricsInventory Inventory);
