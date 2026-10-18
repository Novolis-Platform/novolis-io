namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Raw version response from a factory-firmware Keytron.
/// </summary>
public sealed class KeytronLegacyDeviceInfo
{
    internal KeytronLegacyDeviceInfo(byte[] rawReport)
    {
        RawReport = rawReport;
    }

    /// <summary>
    /// The complete input report, including the report identifier byte.
    /// </summary>
    public IReadOnlyList<byte> RawReport { get; }

    /// <summary>
    /// The command byte returned by the device, when present.
    /// </summary>
    public byte Command => RawReport.Count > 1 ? RawReport[1] : (byte)0;

    /// <summary>
    /// Returns the version response as a copyable hexadecimal string.
    /// </summary>
    public string RawHex => Convert.ToHexString(RawReport.ToArray());
}
