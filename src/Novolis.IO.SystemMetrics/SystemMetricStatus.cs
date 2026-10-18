namespace Novolis.IO.SystemMetrics;

/// <summary>Describes whether a system metric was collected.</summary>
public enum SystemMetricStatus
{
    /// <summary>The metric was collected successfully.</summary>
    Supported,

    /// <summary>The current platform does not expose the metric.</summary>
    Unsupported,

    /// <summary>The platform exposed the metric but access was denied.</summary>
    PermissionDenied,

    /// <summary>The metric could not be read for the current system.</summary>
    Unavailable,

    /// <summary>The value came from an earlier successful read.</summary>
    Stale,

    /// <summary>The metric provider failed while reading the value.</summary>
    Error,
}
