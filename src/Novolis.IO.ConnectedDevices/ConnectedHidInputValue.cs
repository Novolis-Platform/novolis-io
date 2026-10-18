namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// One changed, descriptor-parsed value from a HID input report.
/// </summary>
public sealed record ConnectedHidInputValue
{
    /// <summary>
    /// Data-item index assigned by the report parser.
    /// </summary>
    public int DataIndex { get; init; }

    /// <summary>
    /// Usage identifiers associated with the value.
    /// </summary>
    public IReadOnlyList<uint> Usages { get; init; } = Array.Empty<uint>();

    /// <summary>
    /// Optional designator identifiers associated with the value.
    /// </summary>
    public IReadOnlyList<uint> Designators { get; init; } = Array.Empty<uint>();

    /// <summary>
    /// Whether the value passed the report parser's validity checks.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Whether the value represents a HID null state.
    /// </summary>
    public bool IsNull { get; init; }

    /// <summary>
    /// Logical value, when the parsed value is valid and non-null.
    /// </summary>
    public int? LogicalValue { get; init; }

    /// <summary>
    /// Physical value, when the parsed value is valid and non-null.
    /// </summary>
    public double? PhysicalValue { get; init; }
}
