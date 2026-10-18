using Novolis.IO.SystemMetrics;

namespace Novolis.IO.Unit;

public sealed class SystemMetricsTests
{
    [Test]
    public async Task Capture_StreamsSamplesAndBuildsSummary()
    {
        var provider = new FakeProvider();
        var monitor = new SystemMetricsMonitor(() => provider);
        await using var capture = await monitor.StartAsync(
            new SystemMetricsCaptureOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(1),
                MaximumSamples = 3,
                ProcessId = Environment.ProcessId,
            });
        capture.Mark("render-start", "gpu-0");

        var samples = new List<SystemMetricsSample>();
        await foreach (var sample in capture.Samples)
            samples.Add(sample);

        var summary = await capture.CompleteAsync();

        await Assert.That(samples).Count().IsEqualTo(3);
        await Assert.That(summary.SampleCount).IsEqualTo(3);
        await Assert.That(summary.CpuUtilization!.Minimum).IsEqualTo(10);
        await Assert.That(summary.CpuUtilization.Maximum).IsEqualTo(30);
        await Assert.That(summary.CpuUtilization.Average).IsEqualTo(20);
        await Assert.That(summary.Markers).Count().IsEqualTo(1);
        await Assert.That(summary.Markers[0].Name).IsEqualTo("render-start");
        await Assert.That(summary.Markers[0].ResourceId).IsEqualTo("gpu-0");
        await Assert.That(provider.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Capture_ProviderFailureBecomesDiagnosticSample()
    {
        var provider = new FakeProvider { ThrowOnSample = true };
        var monitor = new SystemMetricsMonitor(() => provider);
        await using var capture = await monitor.StartAsync(
            new SystemMetricsCaptureOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(1),
                MaximumSamples = 1,
            });

        var sample = await capture.Samples.FirstAsync();
        var summary = await capture.CompleteAsync();

        await Assert.That(sample.Cpu.Status).IsEqualTo(SystemMetricStatus.Error);
        await Assert.That(summary.Diagnostics).Count().IsGreaterThan(0);
    }

    [Test]
    public async Task DefaultProvider_CollectsOneSample()
    {
        var monitor = SystemMetricsMonitor.CreateDefault();
        await using var capture = await monitor.StartAsync(
            new SystemMetricsCaptureOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(1),
                MaximumSamples = 1,
            });

        var sample = await capture.Samples.FirstAsync();

        await Assert.That(sample.Timestamp).IsNotEqualTo(default);
        await Assert.That(capture.Inventory.Host.MachineName).IsNotEmpty();
    }

    private sealed class FakeProvider : ISystemMetricsProvider
    {
        private int _sampleIndex;

        public bool ThrowOnSample { get; init; }

        public int DisposeCount { get; private set; }

        public string Name => "fake";

        public ValueTask<SystemMetricsInventory> GetInventoryAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                new SystemMetricsInventory(
                    new SystemHostInfo(
                        "test-host",
                        "test-os",
                        "x64",
                        "test",
                        8),
                    new CpuInfo("test-cpu", 8, 4, null),
                    [
                        new GpuAdapterInfo(
                            "gpu-0",
                            "Test GPU",
                            "test",
                            null,
                            "fake",
                            1024,
                            null),
                    ],
                    [
                        new NetworkAdapterInfo(
                            "net-0",
                            "Test Network",
                            "test",
                            System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,
                            System.Net.NetworkInformation.OperationalStatus.Up,
                            1_000,
                            [],
                            []),
                    ],
                    [
                        new ScreenInfo(
                            "screen-0",
                            "Test Screen",
                            0,
                            0,
                            1920,
                            1080,
                            96,
                            60,
                            true,
                            "gpu-0"),
                    ],
                    []));

        public ValueTask<SystemMetricsSample> SampleAsync(
            SystemMetricsSamplingContext context,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSample)
                throw new InvalidOperationException("sample failed");

            var utilization = (++_sampleIndex) * 10d;
            return ValueTask.FromResult(
                new SystemMetricsSample(
                    context.Timestamp,
                    context.Elapsed,
                    new CpuMetrics(utilization, null, null),
                    new MemoryMetrics(100, 50, 50),
                    null,
                    [
                        new GpuMetrics(
                            "gpu-0",
                            utilization,
                            new Dictionary<string, double>(),
                            100,
                            1024,
                            null,
                            null,
                            null,
                            null),
                    ],
                    [
                        new NetworkMetrics(
                            "net-0",
                            100,
                            100,
                            (long)utilization,
                            (long)utilization,
                            null,
                            null),
                    ],
                    1,
                    null,
                    []));
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
