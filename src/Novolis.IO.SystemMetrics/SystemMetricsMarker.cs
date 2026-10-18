namespace Novolis.IO.SystemMetrics;

/// <summary>Application phase marker recorded alongside a capture.</summary>
public sealed record SystemMetricsMarker(
    string Name,
    DateTimeOffset Timestamp,
    TimeSpan Elapsed,
    string? ResourceId,
    IReadOnlyDictionary<string, string> Metadata);
