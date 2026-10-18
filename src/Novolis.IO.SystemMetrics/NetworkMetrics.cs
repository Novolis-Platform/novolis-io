namespace Novolis.IO.SystemMetrics;

/// <summary>One sampled network-adapter resource reading.</summary>
public sealed record NetworkMetrics(
    string AdapterId,
    long? BytesReceived,
    long? BytesSent,
    long? ReceiveBytesPerSecond,
    long? SendBytesPerSecond,
    long? PacketsReceived,
    long? PacketsSent,
    SystemMetricStatus Status = SystemMetricStatus.Supported);
