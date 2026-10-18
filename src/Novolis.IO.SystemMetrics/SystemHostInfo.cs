namespace Novolis.IO.SystemMetrics;

/// <summary>Stable host information captured with a system inventory.</summary>
public sealed record SystemHostInfo(
    string MachineName,
    string OperatingSystem,
    string Architecture,
    string Framework,
    int LogicalProcessorCount);
