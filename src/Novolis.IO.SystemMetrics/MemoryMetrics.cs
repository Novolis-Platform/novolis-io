namespace Novolis.IO.SystemMetrics;

/// <summary>One sampled system memory reading.</summary>
public sealed record MemoryMetrics(
    long? TotalBytes,
    long? AvailableBytes,
    long? UsedBytes,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
