namespace Novolis.IO.SystemMetrics;

internal sealed class UnsupportedSystemMetricsProvider : PlatformSystemMetricsProvider
{
    public override string Name => "Unsupported";

    protected override ValueTask<(
        CpuInfo Cpu,
        IReadOnlyList<GpuAdapterInfo> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformInventoryAsync(CancellationToken cancellationToken)
    {
        var diagnostic = new SystemMetricDiagnostic(
            nameof(UnsupportedSystemMetricsProvider),
            "The current operating system has no built-in metrics provider.",
            SystemMetricStatus.Unsupported);
        return ValueTask.FromResult((
            new CpuInfo(
                "Unknown",
                Environment.ProcessorCount,
                null,
                null,
                SystemMetricStatus.Unsupported),
            (IReadOnlyList<GpuAdapterInfo>)Array.Empty<GpuAdapterInfo>(),
            (IReadOnlyList<ScreenInfo>)Array.Empty<ScreenInfo>(),
            (IReadOnlyList<SystemMetricDiagnostic>)[diagnostic]));
    }

    protected override ValueTask<(
        CpuMetrics Cpu,
        MemoryMetrics Memory,
        IReadOnlyList<GpuMetrics> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformSampleAsync(
            SystemMetricsSamplingContext context,
            CancellationToken cancellationToken)
    {
        var diagnostic = new SystemMetricDiagnostic(
            nameof(UnsupportedSystemMetricsProvider),
            "The current operating system has no built-in metrics provider.",
            SystemMetricStatus.Unsupported);
        return ValueTask.FromResult((
            new CpuMetrics(null, null, null, SystemMetricStatus.Unsupported),
            new MemoryMetrics(null, null, null, SystemMetricStatus.Unsupported),
            (IReadOnlyList<GpuMetrics>)Array.Empty<GpuMetrics>(),
            (IReadOnlyList<ScreenInfo>)Array.Empty<ScreenInfo>(),
            (IReadOnlyList<SystemMetricDiagnostic>)[diagnostic]));
    }
}
