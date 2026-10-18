namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Optional criteria for selecting connected HID interfaces.
/// </summary>
public sealed record ConnectedDeviceFilter
{
    /// <summary>
    /// A filter that matches every HID interface.
    /// </summary>
    public static ConnectedDeviceFilter Any { get; } = new();

    /// <summary>
    /// Restricts matches to one USB vendor identifier.
    /// </summary>
    public int? VendorId { get; init; }

    /// <summary>
    /// Restricts matches to one USB product identifier.
    /// </summary>
    public int? ProductId { get; init; }

    /// <summary>
    /// Restricts matches to one USB release number in binary-coded decimal form.
    /// </summary>
    public int? ReleaseNumberBcd { get; init; }

    /// <summary>
    /// Restricts matches to one exact serial number.
    /// </summary>
    public string? SerialNumber { get; init; }

    /// <summary>
    /// Restricts matches to one exact operating-system device path.
    /// </summary>
    public string? DevicePath { get; init; }

    /// <summary>
    /// Restricts matches to a product name containing this text.
    /// </summary>
    public string? ProductNameContains { get; init; }

    /// <summary>
    /// Restricts matches to a manufacturer name containing this text.
    /// </summary>
    public string? ManufacturerContains { get; init; }

    /// <summary>
    /// Restricts matches to a display name containing this text.
    /// </summary>
    public string? FriendlyNameContains { get; init; }

    /// <summary>
    /// Determines whether <paramref name="device"/> satisfies every configured criterion.
    /// </summary>
    public bool Matches(ConnectedDeviceDescriptor device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return (!VendorId.HasValue || device.VendorId == VendorId.Value)
            && (!ProductId.HasValue || device.ProductId == ProductId.Value)
            && (!ReleaseNumberBcd.HasValue || device.ReleaseNumberBcd == ReleaseNumberBcd.Value)
            && (string.IsNullOrWhiteSpace(SerialNumber)
                || string.Equals(device.SerialNumber, SerialNumber, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(DevicePath)
                || string.Equals(device.DevicePath, DevicePath, StringComparison.OrdinalIgnoreCase))
            && Contains(device.ProductName, ProductNameContains)
            && Contains(device.Manufacturer, ManufacturerContains)
            && Contains(device.FriendlyName, FriendlyNameContains);
    }

    private static bool Contains(string? value, string? text) =>
        string.IsNullOrWhiteSpace(text)
        || (value?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
}
