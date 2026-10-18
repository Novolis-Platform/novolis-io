using System.IO;
using HidSharp;

namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Identifies the full 4x4 WebHub/SDCX macro pad with three rotary controls.
/// </summary>
public sealed class WebHubMacroPad
{
    public const int VendorId = 0x6D7D;
    public const int ProductId = 0xDCFC;
    public const uint ConfigurationUsage = 0xFF000001;

    internal WebHubMacroPad(ConnectedHidDevice device)
    {
        Device = device;
    }

    /// <summary>
    /// The matching vendor HID interface. This is the configuration interface,
    /// not the ordinary keyboard collection.
    /// </summary>
    public ConnectedHidDevice Device { get; }

    /// <summary>
    /// True when this interface exposes the 64-byte, report-id-free frame shape
    /// used by the documented WebHub configuration protocol.
    /// </summary>
    public bool SupportsWebHubFrames
    {
        get
        {
            if (Device.Descriptor.MaxInputReportLength < 64
                || Device.Descriptor.MaxOutputReportLength < 64)
            {
                return false;
            }

            try
            {
                var descriptor = Device.GetReportDescriptor();
                return !descriptor.ReportsUseId;
            }
            catch (Exception exception) when (
                exception is IOException
                    or InvalidOperationException
                    or NotSupportedException
                    or ObjectDisposedException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// True when this is the factory-firmware interface: report ID 2 with
    /// 32-byte input/output reports.
    /// </summary>
    public bool SupportsLegacyFrames
    {
        get
        {
            if (Device.Descriptor.MaxInputReportLength < 32
                || Device.Descriptor.MaxOutputReportLength < 32)
            {
                return false;
            }

            try
            {
                var descriptor = Device.GetReportDescriptor();
                var input = descriptor.InputReports.FirstOrDefault();
                var output = descriptor.OutputReports.FirstOrDefault();
                return descriptor.ReportsUseId
                    && input?.ReportId == 2
                    && output?.ReportId == 2
                    && input.Length >= 32
                    && output.Length >= 32;
            }
            catch (Exception exception) when (
                exception is IOException
                    or InvalidOperationException
                    or NotSupportedException
                    or ObjectDisposedException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Explains why the interface can or cannot use the WebHub session.
    /// </summary>
    public string ProtocolSupportDescription =>
        SupportsWebHubFrames
            ? "WebHub 64-byte configuration frames are available."
            : SupportsLegacyFrames
                ? "Factory-firmware 32-byte report-ID-2 action frames are available; " +
                  "the action table can be backed up, rewritten, and read back."
            : $"This interface exposes {Device.Descriptor.MaxOutputReportLength}-byte output " +
              $"and {Device.Descriptor.MaxInputReportLength}-byte input reports; " +
              "the WebHub editor requires 64-byte report-id-free frames.";

    /// <summary>
    /// Sends only the documented device-info request and returns the first raw
    /// response. This is useful for identifying a report-shape mismatch without
    /// writing configuration, profiles, lighting, or firmware.
    /// </summary>
    public WebHubMacroPadProbeResult ProbeReadOnly(
        TimeSpan? timeout = null)
    {
        var nativeDescriptor = Device.GetNativeReportDescriptor();
        var inputReport = nativeDescriptor.InputReports.FirstOrDefault()
            ?? throw new InvalidDataException("The macro-pad interface has no input report.");
        var outputReport = nativeDescriptor.OutputReports.FirstOrDefault()
            ?? throw new InvalidDataException("The macro-pad interface has no output report.");
        var reportsUseId = nativeDescriptor.ReportsUseID;
        var payloadOffset = reportsUseId ? 1 : 0;
        var request = new byte[] { 0x06, 0x05 };
        if (request.Length > outputReport.Length - payloadOffset)
        {
            throw new InvalidDataException("The macro-pad output report is too short.");
        }

        using var stream = Device.OpenNativeStream();
        var receiver = nativeDescriptor.CreateHidDeviceInputReceiver();
        var inputBuffer = new byte[Math.Max(1, nativeDescriptor.MaxInputReportLength)];
        receiver.Start(stream);

        var output = new byte[outputReport.Length];
        if (reportsUseId)
        {
            output[0] = (byte)outputReport.ReportID;
        }

        request.CopyTo(output, payloadOffset);
        stream.Write(output);

        var wait = timeout ?? TimeSpan.FromSeconds(2);
        if (wait <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (!receiver.WaitHandle.WaitOne(wait))
        {
            throw new TimeoutException("The macro pad did not return a read-only probe response.");
        }

        while (receiver.TryRead(inputBuffer, 0, out var response))
        {
            if (response.ReportID != inputReport.ReportID)
            {
                continue;
            }

            var length = Math.Clamp(response.Length, 0, inputBuffer.Length);
            return new WebHubMacroPadProbeResult(
                request,
                inputBuffer.AsSpan(0, length).ToArray(),
                response.ReportID);
        }

        throw new TimeoutException("The macro pad returned no readable probe response.");
    }

    /// <summary>
    /// Finds the exact full-size 4x4 macro pad in the connected-device catalog.
    /// </summary>
    public static WebHubMacroPad? Find(ConnectedDeviceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var device = catalog.FindHidDeviceByUsage(
            new ConnectedDeviceFilter
            {
                VendorId = VendorId,
                ProductId = ProductId,
            },
            ConfigurationUsage);

        return device is null ? null : new WebHubMacroPad(device);
    }

    /// <summary>
    /// Opens a session on the configuration interface.
    /// </summary>
    public WebHubMacroPadSession OpenSession() => new(Device);

    /// <summary>
    /// Opens a session for the known factory-firmware action-table protocol.
    /// </summary>
    public KeytronLegacySession OpenLegacySession() => new(Device);
}
