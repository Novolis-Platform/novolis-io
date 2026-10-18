namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// One received HID input report with its raw bytes and changed parsed values.
/// </summary>
public sealed class ConnectedHidInputReport
{
    internal ConnectedHidInputReport(
        int reportId,
        ReadOnlyMemory<byte> rawData,
        IReadOnlyList<ConnectedHidInputValue> values)
    {
        ReportId = reportId;
        RawData = rawData;
        Values = values;
    }

    /// <summary>
    /// The report identifier, or zero when the interface does not use identifiers.
    /// </summary>
    public int ReportId { get; }

    /// <summary>
    /// The exact report bytes returned by the HID interface.
    /// </summary>
    public ReadOnlyMemory<byte> RawData { get; }

    /// <summary>
    /// Values that changed while this report was parsed.
    /// </summary>
    public IReadOnlyList<ConnectedHidInputValue> Values { get; }

    /// <summary>
    /// Returns a detached copy of the raw report bytes.
    /// </summary>
    public byte[] ToArray() => RawData.ToArray();
}
