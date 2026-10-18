namespace Novolis.IO.SystemMetrics;

internal abstract class PlatformSystemMetricsProvider : ISystemMetricsProvider
{
    private readonly ProcessMetricsSampler _process = new();
    private readonly NetworkMetricsSampler _network = new();
    private SystemMetricsInventory? _inventory;
    private IReadOnlyList<ScreenInfo>? _lastScreens;
    private long _screenTopologyVersion;

    public abstract string Name { get; }

    public static ISystemMetricsProvider Create() =>
        OperatingSystem.IsWindows()
            ? new WindowsSystemMetricsProvider()
            : OperatingSystem.IsLinux()
                ? new LinuxSystemMetricsProvider()
                : OperatingSystem.IsMacOS()
                    ? new MacOsSystemMetricsProvider()
                    : new UnsupportedSystemMetricsProvider();

    public async ValueTask<SystemMetricsInventory> GetInventoryAsync(
        CancellationToken cancellationToken = default)
    {
        if (_inventory is not null)
            return _inventory;

        var platform = await ReadPlatformInventoryAsync(cancellationToken)
            .ConfigureAwait(false);
        var network = NetworkMetricsSampler.ReadInventory();
        var host = new SystemHostInfo(
            Environment.MachineName,
            Environment.OSVersion.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            Environment.Version.ToString(),
            Environment.ProcessorCount);
        _inventory = new SystemMetricsInventory(
            host,
            platform.Cpu,
            platform.Gpus,
            network.Adapters,
            platform.Screens,
            network.Diagnostics
                .Concat(platform.Diagnostics)
                .ToArray());
        return _inventory;
    }

    public async ValueTask<SystemMetricsSample> SampleAsync(
        SystemMetricsSamplingContext context,
        CancellationToken cancellationToken = default)
    {
        var platform = await ReadPlatformSampleAsync(
                context,
                cancellationToken)
            .ConfigureAwait(false);
        var process = _process.Read(context.ProcessId, context.Elapsed);
        var network = _network.Read(context.Elapsed);

        var screensChanged = _lastScreens is null
            || !_lastScreens.SequenceEqual(platform.Screens);
        if (screensChanged)
        {
            _screenTopologyVersion++;
            _lastScreens = platform.Screens;
        }

        return new SystemMetricsSample(
            context.Timestamp,
            context.Elapsed,
            platform.Cpu,
            platform.Memory,
            process,
            platform.Gpus,
            network.Metrics,
            _screenTopologyVersion,
            screensChanged ? platform.Screens : null,
            platform.Diagnostics
                .Concat(network.Diagnostics)
                .ToArray());
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected abstract ValueTask<(
        CpuInfo Cpu,
        IReadOnlyList<GpuAdapterInfo> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformInventoryAsync(CancellationToken cancellationToken);

    protected abstract ValueTask<(
        CpuMetrics Cpu,
        MemoryMetrics Memory,
        IReadOnlyList<GpuMetrics> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformSampleAsync(
            SystemMetricsSamplingContext context,
            CancellationToken cancellationToken);
}
