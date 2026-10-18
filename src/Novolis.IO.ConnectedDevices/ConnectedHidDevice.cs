using System.IO;
using HidSharp;
using HidSharp.Reports;

namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// A currently enumerated HID interface and its raw report access.
/// </summary>
public sealed class ConnectedHidDevice
{
    private readonly HidDevice _device;

    internal ConnectedHidDevice(HidDevice device, ConnectedDeviceDescriptor descriptor)
    {
        _device = device;
        Descriptor = descriptor;
    }

    /// <summary>
    /// Metadata captured when this interface was enumerated.
    /// </summary>
    public ConnectedDeviceDescriptor Descriptor { get; }

    /// <summary>
    /// Opens the HID report stream.
    /// </summary>
    /// <remarks>
    /// The stream reads device-specific input reports. The first byte is the report
    /// identifier when the device uses report identifiers; otherwise it is zero.
    /// </remarks>
    /// <exception cref="IOException">
    /// The interface is no longer present or cannot be opened.
    /// </exception>
    public Stream OpenStream() => _device.Open();

    /// <summary>
    /// Reads and summarizes the interface's HID report descriptor.
    /// </summary>
    public ConnectedHidReportDescriptor GetReportDescriptor() =>
        ConnectedHidReportDescriptor.FromNative(_device.GetReportDescriptor());

    /// <summary>
    /// Opens a receiver that parses future input reports according to the interface's
    /// report descriptor.
    /// </summary>
    public ConnectedHidInputReader OpenInputReader() =>
        ConnectedHidInputReader.Open(_device);

    /// <summary>
    /// Tries to open the HID report stream without throwing when the operating system
    /// owns the interface or the device has disconnected.
    /// </summary>
    public bool TryOpenStream(out Stream? stream)
    {
        if (_device.TryOpen(out var hidStream))
        {
            stream = hidStream;
            return true;
        }

        stream = null;
        return false;
    }

    /// <summary>
    /// Reads the raw HID report descriptor for diagnostics and device-specific parsing.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The current operating system does not expose the raw descriptor.
    /// </exception>
    public byte[] GetRawReportDescriptor() => _device.GetRawReportDescriptor();

    internal HidStream OpenNativeStream() => _device.Open();

    internal ReportDescriptor GetNativeReportDescriptor() => _device.GetReportDescriptor();
}
