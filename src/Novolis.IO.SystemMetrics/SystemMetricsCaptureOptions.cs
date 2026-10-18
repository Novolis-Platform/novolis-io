namespace Novolis.IO.SystemMetrics;

/// <summary>Controls one system metrics capture.</summary>
public sealed class SystemMetricsCaptureOptions
{
    private TimeSpan _sampleInterval = TimeSpan.FromMilliseconds(250);
    private int _sampleBufferCapacity = 256;

    /// <summary>How often the provider is sampled.</summary>
    public TimeSpan SampleInterval
    {
        get => _sampleInterval;
        init => _sampleInterval = value;
    }

    /// <summary>
    /// Process to measure. When omitted, process-specific metrics are not
    /// collected.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>Adapter selected by the workload, when known.</summary>
    public string? SelectedGpuAdapterId { get; init; }

    /// <summary>
    /// Maximum number of samples waiting for a consumer. Sampling applies
    /// backpressure when this buffer is full.
    /// </summary>
    public int SampleBufferCapacity
    {
        get => _sampleBufferCapacity;
        init => _sampleBufferCapacity = value;
    }

    /// <summary>Optional upper bound for samples in the capture.</summary>
    public long? MaximumSamples { get; init; }

    internal void Validate()
    {
        if (_sampleInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(SampleInterval),
                "The sample interval must be greater than zero.");
        if (_sampleBufferCapacity < 1)
            throw new ArgumentOutOfRangeException(
                nameof(SampleBufferCapacity),
                "The sample buffer capacity must be at least one.");
        if (MaximumSamples is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(MaximumSamples),
                "The maximum sample count must be greater than zero.");
        if (ProcessId is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(ProcessId),
                "The process id must be positive.");
    }
}
