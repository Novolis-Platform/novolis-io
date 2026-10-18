<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-io/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-io/) · [Source](https://github.com/Novolis-Platform/novolis-io)
<!-- novolis-pkg-brand:end -->

# Novolis.IO.SystemMetrics

Cross-platform system resource inventory and bounded time-series capture for
diagnosing games, render jobs, and other host workloads.

## Install

```bash
dotnet add package Novolis.IO.SystemMetrics
```

## Quick start

```csharp
using Novolis.IO.SystemMetrics;

var monitor = SystemMetricsMonitor.CreateDefault();
await using var capture = await monitor.StartAsync(
    new SystemMetricsCaptureOptions
    {
        SampleInterval = TimeSpan.FromMilliseconds(250),
        ProcessId = Environment.ProcessId,
        MaximumSamples = 20,
    });

var selectedGpuAdapterId = capture.Inventory.GpuAdapters.Count > 0
    ? capture.Inventory.GpuAdapters[0].Id
    : null;
capture.Mark("render-start", selectedGpuAdapterId);

await foreach (var sample in capture.Samples)
{
    Console.WriteLine(
        $"{sample.Elapsed}: CPU={sample.Cpu.UtilizationPercent:F1}%");
}

var summary = await capture.CompleteAsync();
```

The sample stream is backed by a bounded channel. A slow consumer applies
backpressure instead of allowing an unbounded in-memory capture. Use
`SystemMetricsMarker` values to identify workload phases and selected
resources.

CPU, process, memory, network, GPU, and screen values can be unavailable on a
particular operating system or under a restricted account. Check each
`SystemMetricStatus` and read `SystemMetricDiagnostic` values instead of
treating missing values as zero.

GPU utilization and memory counters are driver-dependent. The default provider
reports adapter inventory wherever the operating system exposes it and returns
an explicit unavailable or unsupported status when a driver does not expose
usage counters.

Screens describe topology: identity, bounds, DPI, refresh rate, primary
status, and adapter association when the platform exposes them. Display
output usage is intentionally outside this package's first capture model.
