namespace Novolis.IO.SystemMetrics;

/// <summary>Bounded statistics for one sampled numeric metric.</summary>
public sealed record MetricStatistics(
    long Count,
    double Minimum,
    double Maximum,
    double Average,
    double P50,
    double P95);
