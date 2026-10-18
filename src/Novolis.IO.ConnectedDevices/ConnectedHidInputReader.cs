using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;

namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Receives HID input reports and exposes both raw bytes and descriptor-parsed values.
/// </summary>
public sealed class ConnectedHidInputReader : IDisposable
{
    private readonly HidStream _stream;
    private readonly HidDeviceInputReceiver _receiver;
    private readonly DeviceItemInputParser[] _parsers;
    private readonly byte[] _buffer;
    private bool _disposed;

    private ConnectedHidInputReader(
        HidStream stream,
        HidDeviceInputReceiver receiver,
        DeviceItemInputParser[] parsers,
        ConnectedHidReportDescriptor descriptor)
    {
        _stream = stream;
        _receiver = receiver;
        _parsers = parsers;
        Descriptor = descriptor;
        _buffer = new byte[Math.Max(1, descriptor.MaxInputReportLength)];
    }

    /// <summary>
    /// Report-descriptor summary for the interface being read.
    /// </summary>
    public ConnectedHidReportDescriptor Descriptor { get; }

    /// <summary>
    /// True while the underlying receiver is running.
    /// </summary>
    public bool IsRunning => !_disposed && _receiver.IsRunning;

    /// <summary>
    /// Waits until a report is available or the timeout expires.
    /// </summary>
    public bool WaitForInput(TimeSpan timeout)
    {
        ThrowIfDisposed();
        return _receiver.WaitHandle.WaitOne(timeout);
    }

    /// <summary>
    /// Dequeues one input report when one is available.
    /// </summary>
    public bool TryRead(out ConnectedHidInputReport? report)
    {
        ThrowIfDisposed();

        if (!_receiver.TryRead(_buffer, 0, out var nativeReport))
        {
            report = null;
            return false;
        }

        var values = new List<ConnectedHidInputValue>();
        foreach (var parser in _parsers)
        {
            if (!parser.TryParseReport(_buffer, 0, nativeReport))
                continue;

            while (parser.HasChanged)
            {
                var index = parser.GetNextChangedIndex();
                var value = parser.GetValue(index);
                values.Add(
                    new ConnectedHidInputValue
                    {
                        DataIndex = value.DataIndex,
                        Usages = value.Usages.ToArray(),
                        Designators = value.Designators.ToArray(),
                        IsValid = value.IsValid,
                        IsNull = value.IsNull,
                        LogicalValue = value.IsValid && !value.IsNull
                            ? value.GetLogicalValue()
                            : null,
                        PhysicalValue = value.IsValid && !value.IsNull
                            ? value.GetPhysicalValue()
                            : null,
                    });
            }
        }

        var length = Math.Clamp(nativeReport.Length, 0, _buffer.Length);
        report = new ConnectedHidInputReport(
            nativeReport.ReportID,
            _buffer.AsMemory(0, length).ToArray(),
            values);
        return true;
    }

    /// <summary>
    /// Stops receiving reports and closes the underlying HID stream.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    internal static ConnectedHidInputReader Open(HidDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var nativeDescriptor = device.GetReportDescriptor();
        var stream = device.Open();
        try
        {
            var receiver = nativeDescriptor.CreateHidDeviceInputReceiver();
            var parsers = nativeDescriptor.DeviceItems
                .Select(item => item.CreateDeviceItemInputParser())
                .ToArray();
            receiver.Start(stream);
            return new(
                stream,
                receiver,
                parsers,
                ConnectedHidReportDescriptor.FromNative(nativeDescriptor));
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
