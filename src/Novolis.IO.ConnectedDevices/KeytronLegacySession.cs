using System.IO;
using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;

namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Reads and writes the known factory-firmware protocol used by the exact
/// 4×4 Keytron (`6D7D:DCFC`).
/// </summary>
/// <remarks>
/// This is deliberately separate from the WebHub/v105 session. Factory firmware
/// uses report ID 2 with 31 data bytes and stores three-byte actions. Writes
/// replace the action table in flash; callers should read, back up, and verify
/// before applying a map. Firmware-update and reset commands are not exposed.
/// </remarks>
public sealed class KeytronLegacySession : IDisposable
{
    private const int DataLength = 31;
    private const int DefaultActionCount = 25;
    private const byte ReadVersionCommand = 0x01;
    private const byte ReadKeyMapCommand = 0x02;
    private const byte WriteKeyMapCommand = 0x82;

    private readonly HidStream _stream;
    private readonly HidDeviceInputReceiver _receiver;
    private readonly byte[] _inputBuffer;
    private readonly int _inputReportId;
    private readonly int _outputReportId;
    private readonly bool _reportsUseId;
    private readonly int _outputReportLength;
    private bool _disposed;

    internal KeytronLegacySession(ConnectedHidDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!new WebHubMacroPad(device).SupportsLegacyFrames)
        {
            throw new NotSupportedException(
                "This interface does not expose the 32-byte factory-firmware report shape.");
        }

        var descriptor = device.GetNativeReportDescriptor();
        var inputReport = descriptor.InputReports.FirstOrDefault()
            ?? throw new InvalidDataException("The Keytron interface has no input report.");
        var outputReport = descriptor.OutputReports.FirstOrDefault()
            ?? throw new InvalidDataException("The Keytron interface has no output report.");

        _reportsUseId = descriptor.ReportsUseID;
        _inputReportId = inputReport.ReportID;
        _outputReportId = outputReport.ReportID;
        _outputReportLength = outputReport.Length;
        if (!_reportsUseId
            || _inputReportId != 2
            || _outputReportId != 2
            || inputReport.Length < DataLength + 1
            || _outputReportLength < DataLength + 1)
        {
            throw new NotSupportedException(
                "The factory Keytron protocol requires report ID 2 and 32-byte reports.");
        }

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
    /// Reads the factory firmware's raw version response.
    /// </summary>
    public KeytronLegacyDeviceInfo ReadDeviceInfo(TimeSpan? timeout = null)
    {
        var response = SendAndReceive(
            [ReadVersionCommand],
            timeout ?? TimeSpan.FromSeconds(2));
        return new KeytronLegacyDeviceInfo(
            [ (byte)_inputReportId, .. response ]);
    }

    /// <summary>
    /// Reads the 16 key and nine knob action slots.
    /// </summary>
    public IReadOnlyList<KeytronLegacyAction> ReadActions(
        int actionCount = DefaultActionCount,
        TimeSpan? timeout = null)
    {
        if (actionCount is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(actionCount));
        }

        var requiredLength = checked(actionCount * 3);
        var request =
            new byte[]
            {
                ReadKeyMapCommand,
                0x00,
                0x04,
                0x00,
                0x00,
                (byte)(requiredLength >> 8),
                (byte)requiredLength,
            };
        var raw = ReadMapBytes(
            request,
            requiredLength,
            timeout ?? TimeSpan.FromSeconds(2));

        var actions = new KeytronLegacyAction[actionCount];
        for (var index = 0; index < actionCount; index++)
        {
            actions[index] = KeytronLegacyAction.FromBytes(
                raw.AsSpan(index * 3, 3));
        }

        return actions;
    }

    /// <summary>
    /// Replaces the selected action in flash and leaves verification to the caller.
    /// </summary>
    public void WriteAction(
        int actionIndex,
        KeytronLegacyAction action,
        TimeSpan? timeout = null)
    {
        if (actionIndex is < 0 or >= DefaultActionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(actionIndex));
        }

        var actions = ReadActions(DefaultActionCount, timeout);
        var copy = actions.ToArray();
        copy[actionIndex] = action;
        WriteActions(copy, timeout);
    }

    /// <summary>
    /// Writes a complete factory-firmware action table using the documented
    /// length-prefixed stream and waits for every acknowledgement.
    /// </summary>
    public void WriteActions(
        IReadOnlyList<KeytronLegacyAction> actions,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (actions.Count is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(actions));
        }

        var raw = new byte[checked(actions.Count * 3)];
        for (var index = 0; index < actions.Count; index++)
        {
            actions[index].ToByteArray().CopyTo(raw, index * 3);
        }

        var wait = timeout ?? TimeSpan.FromSeconds(2);
        var header =
            new byte[]
            {
                WriteKeyMapCommand,
                0x00,
                0x04,
                0x00,
                0x00,
                (byte)(raw.Length >> 8),
                (byte)raw.Length,
            };
        SendAndRequireAck(header, wait);

        for (var offset = 0; offset < raw.Length; offset += DataLength)
        {
            var count = Math.Min(DataLength, raw.Length - offset);
            var chunk = new byte[DataLength];
            raw.AsSpan(offset, count).CopyTo(chunk);
            SendAndRequireAck(chunk, wait);
        }
    }

    /// <summary>
    /// Stops receiving reports and closes the configuration interface.
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

    private byte[] ReadMapBytes(
        byte[] request,
        int requiredLength,
        TimeSpan timeout)
    {
        ThrowIfDisposed();
        Drain();
        Send(request);

        var bytes = new List<byte>(requiredLength);
        var deadline = DateTime.UtcNow + timeout;
        var first = true;
        while (bytes.Count < requiredLength)
        {
            var response = Receive(deadline);
            if (first && IsPaddedEcho(response, request))
            {
                first = false;
                continue;
            }

            first = false;
            bytes.AddRange(response);
        }

        return bytes.Take(requiredLength).ToArray();
    }

    private byte[] SendAndReceive(byte[] payload, TimeSpan timeout)
    {
        ThrowIfDisposed();
        Drain();
        Send(payload);
        return Receive(DateTime.UtcNow + timeout);
    }

    private void SendAndRequireAck(byte[] payload, TimeSpan timeout)
    {
        var response = SendAndReceive(payload, timeout);
        if (response.Length < 4
            || response[0] != WriteKeyMapCommand
            || response[1] != 0x00
            || response[2] != 0x01
            || response[3] != 0x01)
        {
            throw new InvalidDataException(
                $"Unexpected factory-firmware write acknowledgement: " +
                $"{Convert.ToHexString(response)}");
        }
    }

    private byte[] Receive(DateTime deadline)
    {
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero || !_receiver.WaitHandle.WaitOne(remaining))
            {
                throw new TimeoutException(
                    "The factory-firmware Keytron did not return a response.");
            }

            while (_receiver.TryRead(_inputBuffer, 0, out var report))
            {
                if (report.ReportID != _inputReportId)
                {
                    continue;
                }

                var length = Math.Clamp(report.Length, 0, _inputBuffer.Length);
                var payloadOffset = _reportsUseId ? 1 : 0;
                if (length <= payloadOffset)
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
        if (payload.Length > DataLength)
        {
            throw new ArgumentException(
                $"The factory-firmware payload cannot exceed {DataLength} bytes.",
                nameof(payload));
        }

        var report = new byte[_outputReportLength];
        report[0] = (byte)_outputReportId;
        payload.CopyTo(report, 1);
        _stream.Write(report);
    }

    private void Drain()
    {
        while (_receiver.TryRead(_inputBuffer, 0, out _))
        {
        }
    }

    private static bool IsPaddedEcho(
        ReadOnlySpan<byte> response,
        ReadOnlySpan<byte> request)
    {
        if (response.Length < DataLength || request.Length > DataLength)
        {
            return false;
        }

        for (var index = 0; index < DataLength; index++)
        {
            var expected = index < request.Length ? request[index] : (byte)0;
            if (response[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
