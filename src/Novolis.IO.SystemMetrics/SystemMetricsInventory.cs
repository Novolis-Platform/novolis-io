namespace Novolis.IO.SystemMetrics;

/// <summary>Static resources discovered before a capture starts.</summary>
public sealed record SystemMetricsInventory(
    SystemHostInfo Host,
    CpuInfo Cpu,
    IReadOnlyList<GpuAdapterInfo> GpuAdapters,
    IReadOnlyList<NetworkAdapterInfo> NetworkAdapters,
    IReadOnlyList<ScreenInfo> Screens,
    IReadOnlyList<SystemMetricDiagnostic> Diagnostics)
{
    /// <summary>Creates an empty inventory for a failed or unsupported probe.</summary>
    public static SystemMetricsInventory Empty { get; } = new(
        new SystemHostInfo(
            Environment.MachineName,
            Environment.OSVersion.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            Environment.Version.ToString(),
            Environment.ProcessorCount),
        new CpuInfo(
            "Unknown",
            Environment.ProcessorCount,
            null,
            null,
            SystemMetricStatus.Unavailable),
        Array.Empty<GpuAdapterInfo>(),
        Array.Empty<NetworkAdapterInfo>(),
        Array.Empty<ScreenInfo>(),
        Array.Empty<SystemMetricDiagnostic>());
}
