namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Stable metadata captured from one connected HID interface.
/// </summary>
public sealed record ConnectedDeviceDescriptor
{
    /// <summary>
    /// Operating-system device path. Use this to distinguish identical interfaces.
    /// </summary>
    public string DevicePath { get; init; } = string.Empty;

    /// <summary>
    /// USB vendor identifier.
    /// </summary>
    public int VendorId { get; init; }

    /// <summary>
    /// USB product identifier.
    /// </summary>
    public int ProductId { get; init; }

    /// <summary>
    /// USB release number in binary-coded decimal form.
    /// </summary>
    public int ReleaseNumberBcd { get; init; }

    /// <summary>
    /// Reported manufacturer, when available.
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// Reported product name, when available.
    /// </summary>
    public string? ProductName { get; init; }

    /// <summary>
    /// Reported serial number, when available.
    /// </summary>
    public string? SerialNumber { get; init; }

    /// <summary>
    /// Display-oriented name supplied by the HID implementation.
    /// </summary>
    public string FriendlyName { get; init; } = string.Empty;

    /// <summary>
    /// Maximum input report length, including the report identifier byte.
    /// </summary>
    public int MaxInputReportLength { get; init; }

    /// <summary>
    /// Maximum output report length, including the report identifier byte.
    /// </summary>
    public int MaxOutputReportLength { get; init; }

    /// <summary>
    /// Maximum feature report length, including the report identifier byte.
    /// </summary>
    public int MaxFeatureReportLength { get; init; }

    /// <summary>
    /// Vendor/product identifier formatted for logs and configuration.
    /// </summary>
    public string HardwareId => $"{VendorId:X4}:{ProductId:X4}";
}
