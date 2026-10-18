namespace Novolis.IO.SystemMetrics;

/// <summary>One sampled graphics-adapter resource reading.</summary>
public sealed record GpuMetrics(
    string AdapterId,
    double? UtilizationPercent,
    IReadOnlyDictionary<string, double> EngineUtilizationPercent,
    long? DedicatedMemoryUsedBytes,
    long? DedicatedMemoryTotalBytes,
    long? SharedMemoryUsedBytes,
    long? SharedMemoryTotalBytes,
    double? TemperatureCelsius,
    double? PowerWatts,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
