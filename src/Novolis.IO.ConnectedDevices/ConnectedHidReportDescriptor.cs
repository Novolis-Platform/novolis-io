using HidSharp.Reports;

namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Platform-neutral summary of a HID report descriptor.
/// </summary>
public sealed record ConnectedHidReportDescriptor
{
    /// <summary>
    /// True when reports include an explicit report identifier byte.
    /// </summary>
    public bool ReportsUseId { get; init; }

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
    /// Logical collections declared by the interface.
    /// </summary>
    public IReadOnlyList<ConnectedHidCollection> Collections { get; init; } =
        Array.Empty<ConnectedHidCollection>();

    /// <summary>
    /// All input reports declared by the interface.
    /// </summary>
    public IReadOnlyList<ConnectedHidReport> InputReports { get; init; } =
        Array.Empty<ConnectedHidReport>();

    /// <summary>
    /// All output reports declared by the interface.
    /// </summary>
    public IReadOnlyList<ConnectedHidReport> OutputReports { get; init; } =
        Array.Empty<ConnectedHidReport>();

    internal static ConnectedHidReportDescriptor FromNative(ReportDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var inputReports = descriptor.InputReports
            .Select(FromNative)
            .ToArray();
        var outputReports = descriptor.OutputReports
            .Select(FromNative)
            .ToArray();
        var collections = descriptor.DeviceItems
            .Select(
                item => new ConnectedHidCollection
                {
                    CollectionType = item.CollectionType.ToString(),
                    Usages = item.Usages.GetAllValues().ToArray(),
                    InputReports = item.InputReports
                        .Select(FromNative)
                        .ToArray(),
                    OutputReports = item.OutputReports
                        .Select(FromNative)
                        .ToArray(),
                })
            .ToArray();

        return new ConnectedHidReportDescriptor
        {
            ReportsUseId = descriptor.ReportsUseID,
            MaxInputReportLength = descriptor.MaxInputReportLength,
            MaxOutputReportLength = descriptor.MaxOutputReportLength,
            MaxFeatureReportLength = descriptor.MaxFeatureReportLength,
            Collections = collections,
            InputReports = inputReports,
            OutputReports = outputReports,
        };
    }

    private static ConnectedHidReport FromNative(Report report) =>
        new()
        {
            ReportId = report.ReportID,
            Length = report.Length,
            Usages = report.GetAllUsages().ToArray(),
        };
}
