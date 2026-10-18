using System.Net.NetworkInformation;

namespace Novolis.IO.SystemMetrics;

internal sealed class NetworkMetricsSampler
{
    private readonly Dictionary<string, (long Received, long Sent, long PacketsReceived, long PacketsSent)> _previous =
        new(StringComparer.Ordinal);

    public static (
        IReadOnlyList<NetworkAdapterInfo> Adapters,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics) ReadInventory()
    {
        var adapters = new List<NetworkAdapterInfo>();
        var diagnostics = new List<SystemMetricDiagnostic>();

        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(NetworkMetricsSampler),
                ex.Message,
                SystemMetricStatus.Unavailable));
            return (adapters, diagnostics);
        }

        foreach (var networkInterface in interfaces)
        {
            try
            {
                var properties = networkInterface.GetIPProperties();
                adapters.Add(new NetworkAdapterInfo(
                    networkInterface.Id,
                    networkInterface.Name,
                    networkInterface.Description,
                    networkInterface.NetworkInterfaceType,
                    networkInterface.OperationalStatus,
                    ReadLinkSpeed(networkInterface),
                    properties.UnicastAddresses
                        .Select(address => address.Address)
                        .ToArray(),
                    properties.GatewayAddresses
                        .Select(gateway => gateway.Address)
                        .ToArray()));
            }
            catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException)
            {
                diagnostics.Add(new SystemMetricDiagnostic(
                    nameof(NetworkMetricsSampler),
                    ex.Message,
                    SystemMetricStatus.Unavailable,
                    networkInterface.Id));
            }
        }

        return (adapters, diagnostics);
    }

    public (
        IReadOnlyList<NetworkMetrics> Metrics,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics) Read(
        TimeSpan elapsed)
    {
        var metrics = new List<NetworkMetrics>();
        var diagnostics = new List<SystemMetricDiagnostic>();

        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(NetworkMetricsSampler),
                ex.Message,
                SystemMetricStatus.Unavailable));
            return (metrics, diagnostics);
        }

        foreach (var networkInterface in interfaces)
        {
            try
            {
                var statistics = networkInterface.GetIPStatistics();
                var received = statistics.BytesReceived;
                var sent = statistics.BytesSent;
                var packetsReceived = statistics.UnicastPacketsReceived;
                var packetsSent = statistics.UnicastPacketsSent;
                _previous.TryGetValue(
                    networkInterface.Id,
                    out var previous);

                metrics.Add(new NetworkMetrics(
                    networkInterface.Id,
                    received,
                    sent,
                    Rate(received, previous.Received, elapsed),
                    Rate(sent, previous.Sent, elapsed),
                    packetsReceived,
                    packetsSent));
                _previous[networkInterface.Id] =
                    (received, sent, packetsReceived, packetsSent);
            }
            catch (Exception ex) when (
                ex is NetworkInformationException
                    or InvalidOperationException
                    or PlatformNotSupportedException)
            {
                diagnostics.Add(new SystemMetricDiagnostic(
                    nameof(NetworkMetricsSampler),
                    ex.Message,
                    SystemMetricStatus.Unavailable,
                    networkInterface.Id));
                metrics.Add(new NetworkMetrics(
                    networkInterface.Id,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    SystemMetricStatus.Unavailable));
            }
        }

        return (metrics, diagnostics);
    }

    private static long? Rate(long current, long previous, TimeSpan elapsed)
    {
        if (previous <= 0 || current < previous || elapsed <= TimeSpan.Zero)
            return null;

        var bytes = current - previous;
        return (long)Math.Round(bytes / elapsed.TotalSeconds);
    }

    private static long? ReadLinkSpeed(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.Speed >= 0
                ? networkInterface.Speed
                : null;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }
}
