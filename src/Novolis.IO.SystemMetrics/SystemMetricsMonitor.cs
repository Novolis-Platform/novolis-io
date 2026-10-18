namespace Novolis.IO.SystemMetrics;

/// <summary>Creates independent system metrics captures.</summary>
public sealed class SystemMetricsMonitor
{
    private readonly Func<ISystemMetricsProvider> _providerFactory;

    /// <summary>Creates a monitor using a provider factory.</summary>
    public SystemMetricsMonitor(Func<ISystemMetricsProvider> providerFactory)
    {
        _providerFactory = providerFactory
            ?? throw new ArgumentNullException(nameof(providerFactory));
    }

    /// <summary>Creates a monitor using the current operating system.</summary>
    public static SystemMetricsMonitor CreateDefault() =>
        new(PlatformSystemMetricsProvider.Create);

    /// <summary>Starts a new capture.</summary>
    public async ValueTask<SystemMetricsCapture> StartAsync(
        SystemMetricsCaptureOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SystemMetricsCaptureOptions();
        options.Validate();

        var provider = _providerFactory()
            ?? throw new InvalidOperationException(
                "The system metrics provider factory returned null.");
        try
        {
            var inventory = await provider
                .GetInventoryAsync(cancellationToken)
                .ConfigureAwait(false);
            return SystemMetricsCapture.Start(provider, inventory, options);
        }
        catch
        {
            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
