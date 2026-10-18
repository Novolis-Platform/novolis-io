namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Describes one logical HID application collection.
/// </summary>
public sealed record ConnectedHidCollection
{
    /// <summary>
    /// The HID collection type reported by the descriptor.
    /// </summary>
    public string CollectionType { get; init; } = string.Empty;

    /// <summary>
    /// Usage identifiers assigned to the collection.
    /// </summary>
    public IReadOnlyList<uint> Usages { get; init; } = Array.Empty<uint>();

    /// <summary>
    /// Input reports belonging to the collection.
    /// </summary>
    public IReadOnlyList<ConnectedHidReport> InputReports { get; init; } =
        Array.Empty<ConnectedHidReport>();

    /// <summary>
    /// Output reports belonging to this collection.
    /// </summary>
    public IReadOnlyList<ConnectedHidReport> OutputReports { get; init; } =
        Array.Empty<ConnectedHidReport>();
}
