namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// The read-only information returned by the macro pad's device-info command.
/// </summary>
public sealed class WebHubMacroPadDeviceInfo
{
    internal WebHubMacroPadDeviceInfo(
        byte[] rawData,
        byte buttonFunctionLength,
        byte activeProfile)
    {
        RawData = rawData;
        ButtonFunctionLength = buttonFunctionLength;
        ActiveProfile = activeProfile;
    }

    /// <summary>
    /// The response payload excluding the HID report identifier.
    /// </summary>
    public IReadOnlyList<byte> RawData { get; }

    /// <summary>
    /// Number of bytes used by one button/knob action record.
    /// </summary>
    public byte ButtonFunctionLength { get; }

    /// <summary>
    /// Profile selected by the device.
    /// </summary>
    public byte ActiveProfile { get; }
}
