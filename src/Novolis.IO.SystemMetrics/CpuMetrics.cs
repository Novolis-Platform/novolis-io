namespace Novolis.IO.SystemMetrics;

/// <summary>One sampled system CPU reading.</summary>
public sealed record CpuMetrics(
    double? UtilizationPercent,
    long? TotalProcessorTimeTicks,
    long? IdleProcessorTimeTicks,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
