namespace Novolis.IO.SystemMetrics;

/// <summary>Explains a partial or failed system metric read.</summary>
public sealed record SystemMetricDiagnostic(
    string Source,
    string Message,
    SystemMetricStatus Status = SystemMetricStatus.Unavailable,
    string? ResourceId = null);
