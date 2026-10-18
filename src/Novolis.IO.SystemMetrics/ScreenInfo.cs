namespace Novolis.IO.SystemMetrics;

/// <summary>Static topology information for one screen or monitor.</summary>
public sealed record ScreenInfo(
    string Id,
    string Name,
    int Left,
    int Top,
    int Width,
    int Height,
    uint? Dpi,
    double? RefreshRateHertz,
    bool IsPrimary,
    string? AdapterId,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
