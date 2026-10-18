namespace Novolis.IO.SystemMetrics;

/// <summary>One sampled process resource reading.</summary>
public sealed record ProcessMetrics(
    int ProcessId,
    string? ProcessName,
    double? CpuUtilizationPercent,
    TimeSpan? TotalProcessorTime,
    long? WorkingSetBytes,
    long? PrivateMemoryBytes,
    int? ThreadCount,
    long? ReadBytesPerSecond,
    long? WriteBytesPerSecond,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
