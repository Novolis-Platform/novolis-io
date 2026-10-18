using System.Net;
using System.Net.NetworkInformation;

namespace Novolis.IO.SystemMetrics;

/// <summary>Static information for one network adapter.</summary>
public sealed record NetworkAdapterInfo(
    string Id,
    string Name,
    string Description,
    NetworkInterfaceType InterfaceType,
    OperationalStatus Status,
    long? LinkSpeedBitsPerSecond,
    IReadOnlyList<IPAddress> Addresses,
    IReadOnlyList<IPAddress> Gateways);
