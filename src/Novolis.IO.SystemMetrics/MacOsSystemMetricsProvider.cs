using System.Runtime.InteropServices;

namespace Novolis.IO.SystemMetrics;

internal sealed class MacOsSystemMetricsProvider : PlatformSystemMetricsProvider
{
    private readonly string _cpuName =
        ReadSysctlString("machdep.cpu.brand_string") ?? "macOS CPU";
    private long? _totalMemory;
    private long? _previousCpuTotal;
    private long? _previousCpuIdle;

    public override string Name => "macOS";

    protected override ValueTask<(
        CpuInfo Cpu,
        IReadOnlyList<GpuAdapterInfo> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformInventoryAsync(CancellationToken cancellationToken)
    {
        var diagnostics = new List<SystemMetricDiagnostic>();
        var screens = ReadScreens(diagnostics);
        _totalMemory = ReadSysctlLong("hw.memsize");
        var gpus = Array.Empty<GpuAdapterInfo>();
        diagnostics.Add(new SystemMetricDiagnostic(
            nameof(MacOsSystemMetricsProvider),
            "Per-adapter GPU inventory requires a Metal or IOKit provider.",
            SystemMetricStatus.Unsupported));
        return ValueTask.FromResult((
            new CpuInfo(
                _cpuName,
                Environment.ProcessorCount,
                null,
                null),
            (IReadOnlyList<GpuAdapterInfo>)gpus,
            screens,
            (IReadOnlyList<SystemMetricDiagnostic>)diagnostics));
    }

    protected override ValueTask<(
        CpuMetrics Cpu,
        MemoryMetrics Memory,
        IReadOnlyList<GpuMetrics> Gpus,
        IReadOnlyList<ScreenInfo> Screens,
        IReadOnlyList<SystemMetricDiagnostic> Diagnostics)>
        ReadPlatformSampleAsync(
            SystemMetricsSamplingContext context,
            CancellationToken cancellationToken)
    {
        var diagnostics = new List<SystemMetricDiagnostic>();
        var memory = _totalMemory is { } total
            ? new MemoryMetrics(
                total,
                null,
                null,
                SystemMetricStatus.Supported)
            : new MemoryMetrics(
                null,
                null,
                null,
                SystemMetricStatus.Unavailable);
        if (_totalMemory is null)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(MacOsSystemMetricsProvider),
                "The macOS physical memory size was unavailable.",
                SystemMetricStatus.Unavailable));
        }

        return ValueTask.FromResult((
            ReadCpu(diagnostics),
            memory,
            (IReadOnlyList<GpuMetrics>)Array.Empty<GpuMetrics>(),
            ReadScreens(diagnostics),
            (IReadOnlyList<SystemMetricDiagnostic>)diagnostics));
    }

    private CpuMetrics ReadCpu(List<SystemMetricDiagnostic> diagnostics)
    {
        try
        {
            const int CpuLoadFlavor = 3;
            const int CpuLoadCount = 4;
            var host = machHostSelf();
            var buffer = Marshal.AllocHGlobal(CpuLoadCount * sizeof(uint));
            try
            {
                uint count = CpuLoadCount;
                var result = hostStatistics(
                    host,
                    CpuLoadFlavor,
                    buffer,
                    ref count);
                if (result != 0 || count < CpuLoadCount)
                    throw new InvalidOperationException(
                        $"host_statistics returned {result}.");

                var user = (long)(uint)Marshal.ReadInt32(buffer, 0);
                var system = (long)(uint)Marshal.ReadInt32(
                    buffer,
                    sizeof(uint));
                var nice = (long)(uint)Marshal.ReadInt32(
                    buffer,
                    sizeof(uint) * 2);
                var idle = (long)(uint)Marshal.ReadInt32(
                    buffer,
                    sizeof(uint) * 3);
                var total = user + system + nice + idle;
                double? utilization = _previousCpuTotal is { } previousTotal
                    && _previousCpuIdle is { } previousIdle
                    && total > previousTotal
                    && idle >= previousIdle
                    ? Math.Clamp(
                        (total - previousTotal - (idle - previousIdle))
                            / (double)(total - previousTotal)
                            * 100,
                        0,
                        100)
                    : null;
                _previousCpuTotal = total;
                _previousCpuIdle = idle;
                return new CpuMetrics(utilization, total, idle);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(MacOsSystemMetricsProvider),
                "macOS host CPU statistics were unavailable.",
                SystemMetricStatus.Unavailable));
            return new CpuMetrics(
                null,
                null,
                null,
                SystemMetricStatus.Unavailable);
        }
    }

    private static IReadOnlyList<ScreenInfo> ReadScreens(
        List<SystemMetricDiagnostic> diagnostics)
    {
        var screens = new List<ScreenInfo>();
        try
        {
            var displays = new uint[32];
            var result = CGGetActiveDisplayList(
                (uint)displays.Length,
                displays,
                out var count);
            if (result != 0)
                throw new InvalidOperationException(
                    $"CGGetActiveDisplayList returned {result}.");

            var mainDisplay = CGMainDisplayID();
            for (var index = 0; index < count; index++)
            {
                var display = displays[index];
                var bounds = CGDisplayBounds(display);
                var mode = CGDisplayCopyDisplayMode(display);
                double? refresh = mode == IntPtr.Zero
                    ? null
                    : CGDisplayModeGetRefreshRate(mode);
                if (mode != IntPtr.Zero)
                    CGDisplayModeRelease(mode);

                screens.Add(new ScreenInfo(
                    $"mac-display-{display}",
                    $"Display {display}",
                    (int)Math.Round(bounds.X),
                    (int)Math.Round(bounds.Y),
                    (int)Math.Round(bounds.Width),
                    (int)Math.Round(bounds.Height),
                    null,
                    refresh > 0 ? refresh : null,
                    display == mainDisplay,
                    null));
            }
        }
        catch (Exception)
        {
            diagnostics.Add(new SystemMetricDiagnostic(
                nameof(MacOsSystemMetricsProvider),
                "CoreGraphics display topology was unavailable.",
                SystemMetricStatus.Unavailable));
        }

        return screens;
    }

    private static long? ReadSysctlLong(string name)
    {
        var size = (nuint)sizeof(long);
        var value = 0L;
        try
        {
            return sysctlbyname(
                    name,
                    ref value,
                    ref size,
                    IntPtr.Zero,
                    0)
                == 0
                ? value
                : null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    private static string? ReadSysctlString(string name)
    {
        try
        {
            nuint size = 0;
            if (sysctlbyname(name, IntPtr.Zero, ref size, IntPtr.Zero, 0) != 0
                || size == 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                return sysctlbyname(
                        name,
                        buffer,
                        ref size,
                        IntPtr.Zero,
                        0)
                    == 0
                    ? Marshal.PtrToStringAnsi(buffer)?.TrimEnd('\0')
                    : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("libc")]
    private static extern int sysctlbyname(
        string name,
        ref long oldp,
        ref nuint oldlenp,
        IntPtr newp,
        nuint newlen);

    [DllImport("libc")]
    private static extern int sysctlbyname(
        string name,
        IntPtr oldp,
        ref nuint oldlenp,
        IntPtr newp,
        nuint newlen);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern uint machHostSelf();

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern int hostStatistics(
        uint host,
        int flavor,
        IntPtr hostInfo,
        ref uint hostInfoCount);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGGetActiveDisplayList(
        uint maxDisplays,
        [Out] uint[] activeDisplays,
        out uint displayCount);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern Rectangle CGDisplayBounds(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGDisplayCopyDisplayMode(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern double CGDisplayModeGetRefreshRate(IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGDisplayModeRelease(IntPtr mode);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }
}
