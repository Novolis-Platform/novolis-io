namespace Novolis.IO.SystemMetrics;

/// <summary>Summary produced when a metrics capture completes.</summary>
public sealed record SystemMetricsSummary(
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan Duration,
    long SampleCount,
    MetricStatistics? CpuUtilization,
    MetricStatistics? ProcessCpuUtilization,
    IReadOnlyDictionary<string, MetricStatistics> GpuUtilization,
    IReadOnlyDictionary<string, MetricStatistics> NetworkReceiveBytesPerSecond,
    IReadOnlyDictionary<string, MetricStatistics> NetworkSendBytesPerSecond,
    IReadOnlyList<SystemMetricsMarker> Markers,
    IReadOnlyList<SystemMetricDiagnostic> Diagnostics);
