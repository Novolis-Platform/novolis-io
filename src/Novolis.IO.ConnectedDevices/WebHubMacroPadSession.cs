using System.IO;
using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;

namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// A live configuration session for the WebHub/SDCX macro pad.
/// </summary>
/// <remarks>
/// The session intentionally exposes only supported profile, lighting, and action
/// commands. Factory reset and bootloader commands are not part of this API.
/// </remarks>
public sealed class WebHubMacroPadSession : IDisposable
{
    private const int ConfigurationPageSize = 56;
    private const int ActionRecordSize = 4;
    private const int DefaultActionCount = 25;

    private readonly HidStream _stream;
    private readonly HidDeviceInputReceiver _receiver;
    private readonly byte[] _inputBuffer;
    private readonly int _inputReportId;
    private readonly int _outputReportId;
    private readonly bool _reportsUseId;
    private readonly int _outputReportLength;
    private bool _disposed;

    internal WebHubMacroPadSession(ConnectedHidDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!new WebHubMacroPad(device).SupportsWebHubFrames)
        {
            throw new NotSupportedException(
                "This interface does not expose the 64-byte report shape required by WebHub.");
        }

        var descriptor = device.GetNativeReportDescriptor();
        var inputReport = descriptor.InputReports.FirstOrDefault()
            ?? throw new InvalidDataException("The macro-pad interface has no input report.");
        var outputReport = descriptor.OutputReports.FirstOrDefault()
            ?? throw new InvalidDataException("The macro-pad interface has no output report.");

        _reportsUseId = descriptor.ReportsUseID;
        _inputReportId = inputReport.ReportID;
        _outputReportId = outputReport.ReportID;
        _outputReportLength = outputReport.Length;
        _inputBuffer = new byte[Math.Max(1, descriptor.MaxInputReportLength)];

        _stream = device.OpenNativeStream();
        try
        {
            _receiver = descriptor.CreateHidDeviceInputReceiver();
            _receiver.Start(_stream);
        }
        catch
        {
            _stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the firmware's device information and active profile.
    /// </summary>
    public WebHubMacroPadDeviceInfo ReadDeviceInfo(
        TimeSpan? timeout = null)
    {
        var response = SendAndReceive([0x06, 0x05], timeout ?? TimeSpan.FromSeconds(2));
        EnsureResponse(response, 0x05);

        return new WebHubMacroPadDeviceInfo(
            response,
            response.Length > 13 ? response[13] : (byte)0,
            response.Length > 16 ? response[16] : (byte)0);
    }

    /// <summary>
    /// Reads one 56-byte configuration page. The returned bytes are the complete
    /// response payload, including the protocol header.
    /// </summary>
    public byte[] ReadActionPage(
        int offset,
        TimeSpan? timeout = null) =>
        ReadActionPage(offset, 0, timeout);

    /// <summary>
    /// Reads one 56-byte configuration page from a selected layer.
    /// </summary>
    public byte[] ReadActionPage(
        int offset,
        byte layer,
        TimeSpan? timeout = null)
    {
        if (offset < 0 || offset % ConfigurationPageSize != 0 || offset > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Configuration offsets must be non-negative multiples of 56.");
        }

        var response = SendAndReceive(
            [0x06, 0x08, 0x3A, (byte)offset, (byte)(offset >> 8), 0, layer],
            timeout ?? TimeSpan.FromSeconds(2));
        EnsureResponse(response, 0x07);
        return response;
    }

    /// <summary>
    /// Reads the action records for the 16 keys and three rotary controls.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The device returned shorter pages than the protocol specifies.
    /// </exception>
    public IReadOnlyList<WebHubMacroPadAction> ReadActions(
        int actionCount = DefaultActionCount,
        TimeSpan? timeout = null) =>
        ReadActions(actionCount, 0, timeout);

    /// <summary>
    /// Reads action records from a selected layer.
    /// </summary>
    public IReadOnlyList<WebHubMacroPadAction> ReadActions(
        int actionCount,
        byte layer,
        TimeSpan? timeout = null)
    {
        if (actionCount is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(actionCount));
        }

        var requiredBytes = checked(actionCount * ActionRecordSize);
        var records = new List<byte>(requiredBytes);
        var pageCount = (requiredBytes + ConfigurationPageSize - 1) / ConfigurationPageSize;
        for (var page = 0; page < pageCount; page++)
        {
            var response = ReadActionPage(page * ConfigurationPageSize, layer, timeout);
            if (response.Length < 8)
            {
                throw new InvalidDataException("The macro-pad returned a short action page.");
            }

            records.AddRange(response.AsSpan(8).ToArray());
        }

        if (records.Count < requiredBytes)
        {
            throw new InvalidDataException(
                $"The macro-pad returned {records.Count} action bytes; {requiredBytes} were expected.");
        }

        var actions = new WebHubMacroPadAction[actionCount];
        for (var index = 0; index < actions.Length; index++)
        {
            actions[index] = WebHubMacroPadAction.FromBytes(
                records.GetRange(index * ActionRecordSize, ActionRecordSize).ToArray());
        }

        return actions;
    }

    /// <summary>
    /// Writes one key or rotary-control action. The command is sent only after the
    /// caller has explicitly requested the write.
    /// </summary>
    public void WriteAction(int actionIndex, WebHubMacroPadAction action, byte layer = 0)
    {
        if (actionIndex is < 0 or >= DefaultActionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(actionIndex));
        }

        var offset = checked(actionIndex * ActionRecordSize);
        var command = new byte[16];
        command[0] = 0x06;
        command[1] = 0x10;
        command[2] = 0x07;
        command[3] = (byte)offset;
        command[4] = (byte)(offset >> 8);
        command[6] = layer;
        action.ToByteArray().CopyTo(command, 8);
        Send(command);
    }

    /// <summary>
    /// Selects the active profile.
    /// </summary>
    public void SelectProfile(byte profile)
    {
        var command = new byte[16];
        command[0] = 0x06;
        command[1] = 0xFB;
        command[2] = profile;
        Send(command);
    }

    /// <summary>
    /// Reads the current backlight mode and values.
    /// </summary>
    public byte[] ReadBacklight(TimeSpan? timeout = null)
    {
        var response = SendAndReceive([0x06, 0x0A], timeout ?? TimeSpan.FromSeconds(2));
        EnsureResponse(response, 0x0A);
        return response;
    }

    /// <summary>
    /// Writes the 13 backlight setting bytes after the command header. The
    /// values follow the firmware's mode, brightness, speed, custom, and HSV
    /// byte layout.
    /// </summary>
    public void WriteBacklight(ReadOnlySpan<byte> settings)
    {
        if (settings.Length != 13)
        {
            throw new ArgumentException("Backlight settings require 13 bytes.", nameof(settings));
        }

        var command = new byte[16];
        command[0] = 0x06;
        command[1] = 0x0B;
        command[2] = 0x0B;
        settings.CopyTo(command.AsSpan(3));
        Send(command);
    }

    /// <summary>
    /// Stops receiving reports and closes the configuration session.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    private byte[] SendAndReceive(byte[] payload, TimeSpan timeout)
    {
        ThrowIfDisposed();
        Send(payload);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero || !_receiver.WaitHandle.WaitOne(remaining))
            {
                throw new TimeoutException("The macro pad did not return a response.");
            }

            while (_receiver.TryRead(_inputBuffer, 0, out var report))
            {
                if (report.ReportID != _inputReportId)
                {
                    continue;
                }

                var length = Math.Clamp(report.Length, 0, _inputBuffer.Length);
                var payloadOffset = _reportsUseId ? 1 : 0;
                if (length < payloadOffset)
                {
                    continue;
                }

                return _inputBuffer
                    .AsSpan(payloadOffset, length - payloadOffset)
                    .ToArray();
            }
        }
    }

    private void Send(byte[] payload)
    {
        ThrowIfDisposed();

        var payloadOffset = _reportsUseId ? 1 : 0;
        if (payload.Length > _outputReportLength - payloadOffset)
        {
            throw new ArgumentException(
                $"The command is {payload.Length} bytes, but this interface accepts only {_outputReportLength - payloadOffset} bytes.",
                nameof(payload));
        }

        var report = new byte[_outputReportLength];
        if (_reportsUseId)
        {
            report[0] = (byte)_outputReportId;
        }

        payload.CopyTo(report, payloadOffset);
        _stream.Write(report);
    }

    private static void EnsureResponse(byte[] response, byte command)
    {
        if (response.Length < 2 || response[0] != 0xAA || response[1] != command)
        {
            var received = Convert.ToHexString(response);
            throw new InvalidDataException(
                $"Unexpected macro-pad response for command 0x{command:X2}: {received}");
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
