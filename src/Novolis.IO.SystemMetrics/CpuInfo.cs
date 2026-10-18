namespace Novolis.IO.SystemMetrics;

/// <summary>Static processor information.</summary>
public sealed record CpuInfo(
    string Name,
    int LogicalProcessorCount,
    int? PhysicalCoreCount,
    long? BaseFrequencyHertz,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
