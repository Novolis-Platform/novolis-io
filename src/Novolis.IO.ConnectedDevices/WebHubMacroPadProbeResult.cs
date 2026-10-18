namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// The result of the safe, read-only WebHub device-info probe.
/// </summary>
public sealed class WebHubMacroPadProbeResult
{
    internal WebHubMacroPadProbeResult(
        byte[] request,
        byte[] response,
        int reportId)
    {
        Request = request;
        Response = response;
        ReportId = reportId;
    }

    /// <summary>
    /// The two-byte protocol request, excluding any HID report identifier.
    /// </summary>
    public IReadOnlyList<byte> Request { get; }

    /// <summary>
    /// The first raw input report returned by the interface.
    /// </summary>
    public IReadOnlyList<byte> Response { get; }

    /// <summary>
    /// HID report identifier reported by the interface.
    /// </summary>
    public int ReportId { get; }
}
