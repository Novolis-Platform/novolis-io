namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Describes one HID input report from a report descriptor.
/// </summary>
public sealed record ConnectedHidReport
{
    /// <summary>
    /// The report identifier, or zero when the interface does not use identifiers.
    /// </summary>
    public int ReportId { get; init; }

    /// <summary>
    /// The report length, including the report identifier byte.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// Usage identifiers carried by this report.
    /// </summary>
    public IReadOnlyList<uint> Usages { get; init; } = Array.Empty<uint>();
}
