using System.Diagnostics;
using System.Threading.Channels;

namespace Novolis.IO.SystemMetrics;

/// <summary>Runs and summarizes one bounded system metrics capture.</summary>
public sealed class SystemMetricsCapture : IAsyncDisposable
{
    private readonly ISystemMetricsProvider _provider;
    private readonly SystemMetricsInventory _inventory;
    private readonly SystemMetricsCaptureOptions _options;
    private readonly Channel<SystemMetricsSample> _samples;
    private readonly CancellationTokenSource _stop = new();
    private readonly CaptureStatistics _statistics = new();
    private readonly object _markerGate = new();
    private readonly List<SystemMetricsMarker> _markers = [];
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
    private readonly Task _samplingTask;
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;
    private long _sampleCount;

    private SystemMetricsCapture(
        ISystemMetricsProvider provider,
        SystemMetricsInventory inventory,
        SystemMetricsCaptureOptions options)
    {
        _provider = provider;
        _inventory = inventory;
        _options = options;
        _statistics.AddDiagnostics(inventory.Diagnostics);
        _samples = Channel.CreateBounded<SystemMetricsSample>(
            new BoundedChannelOptions(options.SampleBufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false,
                AllowSynchronousContinuations = false,
            });
        _samplingTask = SampleLoopAsync();
    }

    /// <summary>Static resources discovered when the capture started.</summary>
    public SystemMetricsInventory Inventory => _inventory;

    /// <summary>Capture configuration.</summary>
    public SystemMetricsCaptureOptions Options => _options;

    /// <summary>
    /// Samples produced by the capture. The sequence is intended for one
    /// consumer; the bounded channel applies backpressure for slow readers.
    /// </summary>
    public IAsyncEnumerable<SystemMetricsSample> Samples =>
        _samples.Reader.ReadAllAsync();

    /// <summary>Creates and starts a capture for an already-probed provider.</summary>
    internal static SystemMetricsCapture Start(
        ISystemMetricsProvider provider,
        SystemMetricsInventory inventory,
        SystemMetricsCaptureOptions options) =>
        new(provider, inventory, options);

    /// <summary>Records an application phase or selected resource.</summary>
    public void Mark(
        string name,
        string? resourceId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var marker = new SystemMetricsMarker(
            name,
            DateTimeOffset.UtcNow,
            Stopwatch.GetElapsedTime(_startedTimestamp),
            resourceId,
            metadata is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(metadata, StringComparer.Ordinal));
        lock (_markerGate)
            _markers.Add(marker);
    }

    /// <summary>
    /// Stops sampling, completes the sample stream, disposes the provider, and
    /// returns the bounded capture summary.
    /// </summary>
    public async ValueTask<SystemMetricsSummary> CompleteAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        _stop.Cancel();

        await _samplingTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        await _completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return _statistics.Build(
            _startedAt,
            DateTimeOffset.UtcNow,
            SnapshotMarkers());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _stop.Cancel();
        try
        {
            await _samplingTask.ConfigureAwait(false);
            await _completion.Task.ConfigureAwait(false);
        }
        finally
        {
            _stop.Dispose();
        }
    }

    private async Task SampleLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(_options.SampleInterval);
            while (true)
            {
                if (_options.MaximumSamples is { } maximum
                    && Volatile.Read(ref _sampleCount) >= maximum)
                {
                    break;
                }

                await SampleOnceAsync(_stop.Token).ConfigureAwait(false);

                if (_options.MaximumSamples is { } completed
                    && Volatile.Read(ref _sampleCount) >= completed)
                {
                    break;
                }

                if (!await timer
                        .WaitForNextTickAsync(_stop.Token)
                        .ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _statistics.AddDiagnostic(new SystemMetricDiagnostic(
                _provider.Name,
                ex.Message,
                SystemMetricStatus.Error));
        }
        finally
        {
            _samples.Writer.TryComplete();
            try
            {
                await _provider.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _statistics.AddDiagnostic(new SystemMetricDiagnostic(
                    _provider.Name,
                    ex.Message,
                    SystemMetricStatus.Error));
            }

            _completion.TrySetResult();
        }
    }

    private async ValueTask SampleOnceAsync(CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var elapsed = Stopwatch.GetElapsedTime(_startedTimestamp);
        var context = new SystemMetricsSamplingContext(
            timestamp,
            elapsed,
            _options.ProcessId,
            _options.SelectedGpuAdapterId,
            _inventory);

        SystemMetricsSample sample;
        try
        {
            sample = await _provider
                .SampleAsync(context, cancellationToken)
                .ConfigureAwait(false);
            sample = sample with
            {
                Timestamp = timestamp,
                Elapsed = elapsed,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var diagnostic = new SystemMetricDiagnostic(
                _provider.Name,
                ex.Message,
                SystemMetricStatus.Error);
            _statistics.AddDiagnostic(diagnostic);
            sample = CreateUnavailableSample(timestamp, elapsed, diagnostic);
        }

        _statistics.Add(sample);
        await _samples.Writer
            .WriteAsync(sample, cancellationToken)
            .ConfigureAwait(false);
        Interlocked.Increment(ref _sampleCount);
    }

    private SystemMetricsSample CreateUnavailableSample(
        DateTimeOffset timestamp,
        TimeSpan elapsed,
        SystemMetricDiagnostic diagnostic) =>
        new(
            timestamp,
            elapsed,
            new CpuMetrics(null, null, null, SystemMetricStatus.Error),
            new MemoryMetrics(null, null, null, SystemMetricStatus.Error),
            null,
            _inventory.GpuAdapters
                .Select(adapter => new GpuMetrics(
                    adapter.Id,
                    null,
                    new Dictionary<string, double>(),
                    null,
                    adapter.DedicatedMemoryBytes,
                    null,
                    adapter.SharedMemoryBytes,
                    null,
                    null,
                    SystemMetricStatus.Error))
                .ToArray(),
            _inventory.NetworkAdapters
                .Select(adapter => new NetworkMetrics(
                    adapter.Id,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    SystemMetricStatus.Error))
                .ToArray(),
            0,
            _inventory.Screens,
            [diagnostic]);

    private IReadOnlyList<SystemMetricsMarker> SnapshotMarkers()
    {
        lock (_markerGate)
            return _markers.ToArray();
    }
}
