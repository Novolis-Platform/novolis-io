namespace Novolis.IO.Mobile.Android;

/// <summary>Privacy-safe network and VPN facts collected from one Android device.</summary>
public sealed record AndroidNetworkSnapshot
{
    /// <summary>Whether the connectivity service reported an active default network.</summary>
    public bool HasActiveDefaultNetwork { get; init; }

    /// <summary>Transport selected for the active default network.</summary>
    public string? ActiveTransport { get; init; }

    /// <summary>All transports reported for the active default network.</summary>
    public IReadOnlyList<string> ActiveTransports { get; init; } = [];

    /// <summary>Whether the active default network is a VPN transport.</summary>
    public bool ActiveNetworkIsVpn { get; init; }

    /// <summary>Whether any VPN transport was observed in the connectivity snapshot.</summary>
    public bool VpnTransportObserved { get; init; }

    /// <summary>Whether Android marked the active default network as validated.</summary>
    public bool DefaultNetworkValidated { get; init; }

    /// <summary>Package selected by Android's always-on VPN setting, if any.</summary>
    public string? AlwaysOnVpnPackage { get; init; }

    /// <summary>Whether Android's VPN lockdown setting is enabled.</summary>
    public bool AlwaysOnVpnLockdown { get; init; }

    /// <summary>
    /// Hostnames observed in Android connectivity probes. IP addresses, SSIDs,
    /// routes, and raw connectivity dumps are intentionally not retained.
    /// </summary>
    public IReadOnlyList<string> ObservedDestinations { get; init; } = [];
}
