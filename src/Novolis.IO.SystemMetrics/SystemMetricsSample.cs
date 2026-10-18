namespace Novolis.IO.SystemMetrics;

/// <summary>One point-in-time system resource sample.</summary>
public sealed record SystemMetricsSample(
    DateTimeOffset Timestamp,
    TimeSpan Elapsed,
    CpuMetrics Cpu,
    MemoryMetrics Memory,
    ProcessMetrics? Process,
    IReadOnlyList<GpuMetrics> Gpus,
    IReadOnlyList<NetworkMetrics> Networks,
    long ScreenTopologyVersion,
    IReadOnlyList<ScreenInfo>? Screens,
    IReadOnlyList<SystemMetricDiagnostic> Diagnostics);
