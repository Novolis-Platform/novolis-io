using System.IO;
using HidSharp;

namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Enumerates connected HID interfaces and observes HID hot-plug changes.
/// </summary>
public sealed class ConnectedDeviceCatalog : IDisposable
{
    private readonly DeviceList _deviceList;
    private bool _disposed;

    /// <summary>
    /// Creates a catalog over the local machine's connected devices.
    /// </summary>
    public ConnectedDeviceCatalog()
    {
        _deviceList = DeviceList.Local;
        _deviceList.Changed += OnDeviceListChanged;
    }

    /// <summary>
    /// Raised when a HID interface is connected or disconnected.
    /// Re-enumerate the catalog to obtain the current list.
    /// </summary>
    public event EventHandler? DevicesChanged;

    /// <summary>
    /// Returns the currently connected HID interfaces matching <paramref name="filter"/>.
    /// </summary>
    public IReadOnlyList<ConnectedHidDevice> EnumerateHidDevices(
        ConnectedDeviceFilter? filter = null)
    {
        ThrowIfDisposed();

        var devices = new List<ConnectedHidDevice>();
        foreach (var device in _deviceList.GetHidDevices())
        {
            try
            {
                var connectedDevice = new ConnectedHidDevice(
                    device,
                    CreateDescriptor(device));
                if (filter is null || filter.Matches(connectedDevice.Descriptor))
                    devices.Add(connectedDevice);
            }
            catch (Exception exception) when (IsDeviceReadFailure(exception))
            {
                // A device can disappear between enumeration and metadata reads.
            }
        }

        return devices;
    }

    /// <summary>
    /// Returns the first currently connected HID interface matching <paramref name="filter"/>.
    /// </summary>
    public ConnectedHidDevice? FindHidDevice(ConnectedDeviceFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return EnumerateHidDevices(filter).FirstOrDefault();
    }

    /// <summary>
    /// Returns the first HID interface matching <paramref name="filter"/> whose
    /// descriptor declares <paramref name="collectionUsage"/>.
    /// </summary>
    /// <remarks>
    /// Composite devices often expose keyboard, mouse, joystick, and vendor
    /// collections as separate interfaces. Interfaces whose descriptors cannot
    /// be reconstructed are skipped.
    /// </remarks>
    public ConnectedHidDevice? FindHidDeviceByUsage(
        ConnectedDeviceFilter filter,
        uint collectionUsage)
    {
        ArgumentNullException.ThrowIfNull(filter);

        foreach (var device in EnumerateHidDevices(filter))
        {
            try
            {
                if (device.GetReportDescriptor()
                    .Collections
                    .Any(collection => collection.Usages.Contains(collectionUsage)))
                {
                    return device;
                }
            }
            catch (Exception exception) when (IsDeviceReadFailure(exception))
            {
                // Try the next interface in a composite device.
            }
        }

        return null;
    }

    /// <summary>
    /// Stops listening for hot-plug changes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _deviceList.Changed -= OnDeviceListChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs args) =>
        DevicesChanged?.Invoke(this, EventArgs.Empty);

    private static ConnectedDeviceDescriptor CreateDescriptor(HidDevice device)
    {
        var productName = TryReadString(device.GetProductName);
        var manufacturer = TryReadString(device.GetManufacturer);
        var friendlyName = TryReadString(device.GetFriendlyName)
            ?? productName
            ?? manufacturer
            ?? "HID device";

        return new ConnectedDeviceDescriptor
        {
            DevicePath = device.DevicePath ?? string.Empty,
            VendorId = device.VendorID,
            ProductId = device.ProductID,
            ReleaseNumberBcd = device.ReleaseNumberBcd,
            Manufacturer = manufacturer,
            ProductName = productName,
            SerialNumber = TryReadString(device.GetSerialNumber),
            FriendlyName = friendlyName,
            MaxInputReportLength = TryReadInt(device.GetMaxInputReportLength),
            MaxOutputReportLength = TryReadInt(device.GetMaxOutputReportLength),
            MaxFeatureReportLength = TryReadInt(device.GetMaxFeatureReportLength),
        };
    }

    private static string? TryReadString(Func<string> read)
    {
        try
        {
            var value = read();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception exception) when (IsDeviceReadFailure(exception))
        {
            return null;
        }
    }

    private static int TryReadInt(Func<int> read)
    {
        try
        {
            return Math.Max(0, read());
        }
        catch (Exception exception) when (IsDeviceReadFailure(exception))
        {
            return 0;
        }
    }

    private static bool IsDeviceReadFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException
            or ObjectDisposedException;

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
