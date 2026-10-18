namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Raw response from a factory-firmware Keytron lighting command.
/// </summary>
public sealed class KeytronLegacyLightingResponse
{
    internal KeytronLegacyLightingResponse(byte[] rawReport)
    {
        RawReport = rawReport;
    }

    /// <summary>
    /// The complete input report, including the HID report identifier.
    /// </summary>
    public IReadOnlyList<byte> RawReport { get; }

    /// <summary>
    /// The response as an uppercase hexadecimal string.
    /// </summary>
    public string RawHex => Convert.ToHexString(RawReport.ToArray());

    /// <summary>
    /// The factory lighting command returned by the device.
    /// </summary>
    public byte Command => RawReport.Count > 1 ? RawReport[1] : (byte)0;

    /// <summary>
    /// Tries to extract settings echoed by a lighting write response.
    /// </summary>
    public bool TryGetSettings(out KeytronLegacyLightingSettings settings)
    {
        settings = default;
        var offset = RawReport.Count > 0 && RawReport[0] == 2 ? 1 : 0;
        if (RawReport.Count < offset + 15
            || RawReport[offset] != 0x0B
            || RawReport[offset + 1] != 0x0B)
        {
            return false;
        }

        settings = new KeytronLegacyLightingSettings(
            RawReport[offset + 6],
            RawReport[offset + 7],
            RawReport[offset + 8],
            RawReport[offset + 10] != 0,
            RawReport[offset + 12],
            RawReport[offset + 13],
            RawReport[offset + 14]);
        return true;
    }
}
