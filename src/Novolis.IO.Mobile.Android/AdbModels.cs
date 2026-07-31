namespace Novolis.IO.Mobile.Android;

/// <summary>Connection state reported by <c>adb devices</c>.</summary>
public enum AdbDeviceState
{
    /// <summary>Unrecognized or empty state token.</summary>
    Unknown = 0,

    /// <summary>Ready for commands.</summary>
    Device,

    /// <summary>USB debugging not authorized on the handset.</summary>
    Unauthorized,

    /// <summary>Device is offline.</summary>
    Offline,

    /// <summary>No permissions (common on Linux without udev rules).</summary>
    NoPermissions,

    /// <summary>Bootloader mode.</summary>
    Bootloader,

    /// <summary>Recovery mode.</summary>
    Recovery,

    /// <summary>Sideload mode.</summary>
    Sideload,
}

/// <summary>One row from <c>adb devices -l</c>.</summary>
/// <param name="Serial">Transport id / serial.</param>
/// <param name="State">Parsed connection state.</param>
/// <param name="Product">Optional <c>product:</c> tag.</param>
/// <param name="Model">Optional <c>model:</c> tag.</param>
/// <param name="Device">Optional <c>device:</c> tag.</param>
/// <param name="TransportId">Optional <c>transport_id:</c> tag.</param>
public sealed record AdbDevice(
    string Serial,
    AdbDeviceState State,
    string? Product = null,
    string? Model = null,
    string? Device = null,
    string? TransportId = null);

/// <summary>Battery fields from <c>dumpsys battery</c>.</summary>
public sealed class AndroidBatteryInfo
{
    /// <summary>Charge percent (0–100 scale).</summary>
    public int? Level { get; init; }

    /// <summary>Scale denominator (usually 100).</summary>
    public int? Scale { get; init; }

    /// <summary>BatteryManager status code (2=charging, 3=discharging, 5=full, …).</summary>
    public int? Status { get; init; }

    /// <summary>BatteryManager health code.</summary>
    public int? Health { get; init; }

    /// <summary>Millivolts.</summary>
    public int? VoltageMv { get; init; }

    /// <summary>Tenths of a degree Celsius (369 → 36.9°C).</summary>
    public int? TemperatureTenthsC { get; init; }

    /// <summary>Chemistry string (e.g. Li-ion).</summary>
    public string? Technology { get; init; }

    /// <summary>AC powered.</summary>
    public bool? AcPowered { get; init; }

    /// <summary>USB powered.</summary>
    public bool? UsbPowered { get; init; }

    /// <summary>Wireless powered.</summary>
    public bool? WirelessPowered { get; init; }

    /// <summary>Present flag.</summary>
    public bool? Present { get; init; }

    /// <summary>Temperature in Celsius when tenths are known.</summary>
    public double? TemperatureCelsius =>
        TemperatureTenthsC is int t ? t / 10.0 : null;

    /// <summary>Human status label.</summary>
    public string StatusLabel => Status switch
    {
        1 => "unknown",
        2 => "charging",
        3 => "discharging",
        4 => "not charging",
        5 => "full",
        _ => Status?.ToString() ?? "—",
    };
}

/// <summary>Memory snapshot from <c>/proc/meminfo</c> (kibibytes).</summary>
public sealed class AndroidMemoryInfo
{
    /// <summary>MemTotal kB.</summary>
    public long? MemTotalKb { get; init; }

    /// <summary>MemAvailable kB.</summary>
    public long? MemAvailableKb { get; init; }

    /// <summary>MemFree kB.</summary>
    public long? MemFreeKb { get; init; }

    /// <summary>SwapTotal kB.</summary>
    public long? SwapTotalKb { get; init; }

    /// <summary>SwapFree kB.</summary>
    public long? SwapFreeKb { get; init; }
}

/// <summary>Display size / density from <c>wm</c>.</summary>
public sealed class AndroidDisplayInfo
{
    /// <summary>Physical size line (e.g. 1080x2640).</summary>
    public string? PhysicalSize { get; init; }

    /// <summary>Width pixels when parsed.</summary>
    public int? WidthPx { get; init; }

    /// <summary>Height pixels when parsed.</summary>
    public int? HeightPx { get; init; }

    /// <summary>Density dpi.</summary>
    public int? DensityDpi { get; init; }

    /// <summary>Override density line if present.</summary>
    public string? OverrideDensity { get; init; }
}

/// <summary>One <c>df -h</c> mount row.</summary>
/// <param name="Filesystem">Block device / fs.</param>
/// <param name="Size">Size token.</param>
/// <param name="Used">Used token.</param>
/// <param name="Avail">Available token.</param>
/// <param name="UsePercent">Use% token.</param>
/// <param name="MountedOn">Mount path.</param>
public sealed record AndroidStorageMount(
    string Filesystem,
    string Size,
    string Used,
    string Avail,
    string UsePercent,
    string MountedOn);

/// <summary>Identity + runtime technical snapshot for a connected device.</summary>
public sealed class AndroidDeviceInfo
{
    /// <summary>Transport serial used for the query.</summary>
    public required string Serial { get; init; }

    /// <summary><c>adb get-state</c> result.</summary>
    public required string State { get; init; }

    /// <summary><c>ro.product.model</c>.</summary>
    public string? Model { get; init; }

    /// <summary><c>ro.product.manufacturer</c>.</summary>
    public string? Manufacturer { get; init; }

    /// <summary><c>ro.product.brand</c>.</summary>
    public string? Brand { get; init; }

    /// <summary><c>ro.product.name</c>.</summary>
    public string? ProductName { get; init; }

    /// <summary><c>ro.product.device</c>.</summary>
    public string? Device { get; init; }

    /// <summary><c>ro.product.board</c>.</summary>
    public string? Board { get; init; }

    /// <summary><c>ro.hardware</c>.</summary>
    public string? Hardware { get; init; }

    /// <summary><c>ro.build.version.release</c>.</summary>
    public string? AndroidVersion { get; init; }

    /// <summary><c>ro.build.version.sdk</c>.</summary>
    public string? SdkVersion { get; init; }

    /// <summary><c>ro.product.first_api_level</c>.</summary>
    public string? FirstApiLevel { get; init; }

    /// <summary><c>ro.build.version.security_patch</c>.</summary>
    public string? SecurityPatch { get; init; }

    /// <summary><c>ro.build.display.id</c>.</summary>
    public string? BuildDisplay { get; init; }

    /// <summary><c>ro.build.id</c>.</summary>
    public string? BuildId { get; init; }

    /// <summary><c>ro.build.type</c>.</summary>
    public string? BuildType { get; init; }

    /// <summary><c>ro.build.tags</c>.</summary>
    public string? BuildTags { get; init; }

    /// <summary><c>ro.build.fingerprint</c>.</summary>
    public string? Fingerprint { get; init; }

    /// <summary><c>ro.bootloader</c>.</summary>
    public string? Bootloader { get; init; }

    /// <summary><c>gsm.version.baseband</c> / radio.</summary>
    public string? Baseband { get; init; }

    /// <summary><c>ro.serialno</c> when available.</summary>
    public string? HardwareSerial { get; init; }

    /// <summary><c>settings get secure android_id</c>.</summary>
    public string? AndroidId { get; init; }

    /// <summary><c>ro.product.cpu.abi</c>.</summary>
    public string? Abi { get; init; }

    /// <summary><c>ro.product.cpu.abilist</c>.</summary>
    public string? AbiList { get; init; }

    /// <summary>Processor count from <c>/proc/cpuinfo</c>.</summary>
    public int? CpuCoreCount { get; init; }

    /// <summary>Hardware / model name line from cpuinfo when present.</summary>
    public string? CpuHardware { get; init; }

    /// <summary><c>persist.sys.timezone</c>.</summary>
    public string? Timezone { get; init; }

    /// <summary>Uptime seconds from <c>/proc/uptime</c>.</summary>
    public double? UptimeSeconds { get; init; }

    /// <summary>Battery snapshot.</summary>
    public AndroidBatteryInfo? Battery { get; init; }

    /// <summary>Memory snapshot.</summary>
    public AndroidMemoryInfo? Memory { get; init; }

    /// <summary>Primary display size/density.</summary>
    public AndroidDisplayInfo? Display { get; init; }

    /// <summary>Selected storage mounts.</summary>
    public IReadOnlyList<AndroidStorageMount> Storage { get; init; } = [];

    /// <summary>Raw multi-line extras (optional dumpsys snippets).</summary>
    public string? RawExtras { get; init; }

    /// <summary>Formats a multi-section technical report for UI / logs.</summary>
    public string FormatReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Identity ===");
        sb.AppendLine($"Serial (adb)     {Serial}");
        sb.AppendLine($"State            {State}");
        sb.AppendLine($"Manufacturer     {Manufacturer ?? "—"}");
        sb.AppendLine($"Brand            {Brand ?? "—"}");
        sb.AppendLine($"Model            {Model ?? "—"}");
        sb.AppendLine($"Product          {ProductName ?? "—"}");
        sb.AppendLine($"Device           {Device ?? "—"}");
        sb.AppendLine($"Board            {Board ?? "—"}");
        sb.AppendLine($"Hardware         {Hardware ?? "—"}");
        sb.AppendLine($"HW serial        {HardwareSerial ?? "—"}");
        sb.AppendLine($"Android ID       {AndroidId ?? "—"}");
        sb.AppendLine();
        sb.AppendLine("=== Build / OS ===");
        sb.AppendLine($"Android          {AndroidVersion ?? "—"} (SDK {SdkVersion ?? "—"}, first API {FirstApiLevel ?? "—"})");
        sb.AppendLine($"Security patch   {SecurityPatch ?? "—"}");
        sb.AppendLine($"Build display    {BuildDisplay ?? "—"}");
        sb.AppendLine($"Build id         {BuildId ?? "—"}");
        sb.AppendLine($"Build type/tags  {BuildType ?? "—"} / {BuildTags ?? "—"}");
        sb.AppendLine($"Bootloader       {Bootloader ?? "—"}");
        sb.AppendLine($"Baseband         {Baseband ?? "—"}");
        sb.AppendLine($"Fingerprint      {Fingerprint ?? "—"}");
        sb.AppendLine($"Timezone         {Timezone ?? "—"}");
        if (UptimeSeconds is double up)
            sb.AppendLine($"Uptime           {FormatDuration(up)}");
        sb.AppendLine();
        sb.AppendLine("=== CPU / ABI ===");
        sb.AppendLine($"ABI              {Abi ?? "—"}");
        sb.AppendLine($"ABI list         {AbiList ?? "—"}");
        sb.AppendLine($"CPU cores        {CpuCoreCount?.ToString() ?? "—"}");
        sb.AppendLine($"CPU hardware     {CpuHardware ?? "—"}");
        sb.AppendLine();
        sb.AppendLine("=== Display ===");
        if (Display is { } d)
        {
            sb.AppendLine($"Physical size    {d.PhysicalSize ?? "—"}");
            sb.AppendLine($"Density          {d.DensityDpi?.ToString() ?? "—"} dpi");
            if (!string.IsNullOrWhiteSpace(d.OverrideDensity))
                sb.AppendLine($"Override dens.   {d.OverrideDensity}");
        }
        else
        {
            sb.AppendLine("—");
        }

        sb.AppendLine();
        sb.AppendLine("=== Battery ===");
        if (Battery is { } b)
        {
            sb.AppendLine($"Level            {b.Level?.ToString() ?? "—"}% (scale {b.Scale?.ToString() ?? "—"})");
            sb.AppendLine($"Status           {b.StatusLabel} ({b.Status?.ToString() ?? "—"})");
            sb.AppendLine($"Health           {b.Health?.ToString() ?? "—"}");
            sb.AppendLine($"Voltage          {b.VoltageMv?.ToString() ?? "—"} mV");
            sb.AppendLine($"Temperature      {(b.TemperatureCelsius is double c ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{c:0.0} °C") : "—")}");
            sb.AppendLine($"Technology       {b.Technology ?? "—"}");
            sb.AppendLine($"Power            AC={Yn(b.AcPowered)} USB={Yn(b.UsbPowered)} Wireless={Yn(b.WirelessPowered)} Present={Yn(b.Present)}");
        }
        else
        {
            sb.AppendLine("—");
        }

        sb.AppendLine();
        sb.AppendLine("=== Memory ===");
        if (Memory is { } m)
        {
            sb.AppendLine($"Total            {FormatMb(m.MemTotalKb)}");
            sb.AppendLine($"Available        {FormatMb(m.MemAvailableKb)}");
            sb.AppendLine($"Free             {FormatMb(m.MemFreeKb)}");
            sb.AppendLine($"Swap             {FormatMb(m.SwapTotalKb)} total / {FormatMb(m.SwapFreeKb)} free");
        }
        else
        {
            sb.AppendLine("—");
        }

        sb.AppendLine();
        sb.AppendLine("=== Storage ===");
        if (Storage.Count == 0)
        {
            sb.AppendLine("—");
        }
        else
        {
            foreach (var s in Storage.DistinctBy(x => x.MountedOn, StringComparer.Ordinal))
                sb.AppendLine($"{s.MountedOn,-16} {s.Size,6}  used {s.Used,6}  avail {s.Avail,6}  {s.UsePercent,4}  ({s.Filesystem})");
        }

        if (!string.IsNullOrWhiteSpace(RawExtras))
        {
            sb.AppendLine();
            sb.AppendLine("=== Extras ===");
            sb.AppendLine(RawExtras.TrimEnd());
        }

        return sb.ToString().TrimEnd();
    }

    private static string Yn(bool? v) => v is null ? "?" : v.Value ? "yes" : "no";

    private static string FormatMb(long? kb) =>
        kb is null
            ? "—"
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{kb.Value / 1024.0:0.#} MiB ({kb.Value:N0} kB)");

    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }
}

/// <summary>Outcome of an <see cref="AndroidDebugBridge"/> operation.</summary>
public sealed class AdbOperationResult
{
    /// <summary>Creates a result.</summary>
    public AdbOperationResult(bool ok, string command, string message, AdbProcessResult? process = null)
    {
        Ok = ok;
        Command = command;
        Message = message;
        Process = process;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Ok { get; }

    /// <summary>Logical command name.</summary>
    public string Command { get; }

    /// <summary>Human-readable message.</summary>
    public string Message { get; }

    /// <summary>Underlying process capture, when applicable.</summary>
    public AdbProcessResult? Process { get; }

    /// <summary>Success factory.</summary>
    public static AdbOperationResult Success(string command, string message, AdbProcessResult? process = null) =>
        new(true, command, message, process);

    /// <summary>Failure factory.</summary>
    public static AdbOperationResult Fail(string command, string message, AdbProcessResult? process = null) =>
        new(false, command, message, process);
}
