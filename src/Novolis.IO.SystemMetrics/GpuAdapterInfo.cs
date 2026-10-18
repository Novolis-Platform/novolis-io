namespace Novolis.IO.SystemMetrics;

/// <summary>Static information for one graphics adapter.</summary>
public sealed record GpuAdapterInfo(
    string Id,
    string Name,
    string? Vendor,
    string? DriverVersion,
    string? Backend,
    long? DedicatedMemoryBytes,
    long? SharedMemoryBytes,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
