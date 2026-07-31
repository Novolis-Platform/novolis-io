using System.Diagnostics;
using System.Text;

namespace Novolis.IO.Mobile.Android;

/// <summary>Result of a shell or legacy CLI invocation.</summary>
/// <param name="ExitCode">Process / logical exit code.</param>
/// <param name="StdOut">Captured standard output.</param>
/// <param name="StdErr">Captured standard error.</param>
public sealed record AdbProcessResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>Whether <see cref="ExitCode"/> is zero.</summary>
    public bool Ok => ExitCode == 0;

    /// <summary>Combined diagnostic text (stderr preferred when non-empty).</summary>
    public string Diagnostic =>
        string.IsNullOrWhiteSpace(StdErr) ? StdOut.Trim() : StdErr.Trim();
}

/// <summary>Locates a real <c>adb</c> executable used to host the ADB server daemon.</summary>
public static class AdbLocator
{
    /// <summary>
    /// Resolves <c>adb</c> under <c>ANDROID_HOME</c> / <c>ANDROID_SDK_ROOT</c>, common SDK paths, then PATH.
    /// Always verifies the file exists.
    /// </summary>
    public static string Resolve(string? adbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(adbPath))
        {
            var full = Path.GetFullPath(adbPath);
            if (!File.Exists(full))
                throw new FileNotFoundException($"adb executable not found: {full}", full);
            return full;
        }

        foreach (var root in SdkRoots())
        {
            var candidate = Path.Combine(root, "platform-tools", FileName);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        var onPath = FindOnPath(FileName);
        if (onPath is not null)
            return onPath;

        throw new FileNotFoundException(
            "adb not found. Set ANDROID_HOME (or ANDROID_SDK_ROOT) to an SDK with platform-tools, or put adb on PATH.",
            FileName);
    }

    /// <summary>Executable file name for the current OS.</summary>
    public static string FileName => OperatingSystem.IsWindows() ? "adb.exe" : "adb";

    private static IEnumerable<string> SdkRoots()
    {
        foreach (var key in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }

        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(local, "Android", "Sdk");
        }
        else if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, "Library", "Android", "sdk");
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, "Android", "Sdk");
        }
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch
            {
                // ignore malformed PATH segments
            }
        }

        return null;
    }
}

/// <summary>
/// Escape-hatch runner that shells <c>adb</c> as a process.
/// Prefer <see cref="AndroidDebugBridge"/> protocol APIs; this exists for rare CLI-only verbs.
/// </summary>
public interface IAdbProcessRunner
{
    /// <summary>Path to the <c>adb</c> executable in use.</summary>
    string AdbPath { get; }

    /// <summary>Executes <c>adb</c> with <paramref name="args"/>.</summary>
    AdbProcessResult Run(params string[] args);
}

/// <summary>Default <see cref="IAdbProcessRunner"/> (CLI escape hatch).</summary>
public sealed class ProcessAdbRunner : IAdbProcessRunner
{
    /// <summary>Creates a runner, locating <c>adb</c> automatically when <paramref name="adbPath"/> is null.</summary>
    public ProcessAdbRunner(string? adbPath = null) =>
        AdbPath = AdbLocator.Resolve(adbPath);

    /// <inheritdoc />
    public string AdbPath { get; }

    /// <inheritdoc />
    public AdbProcessResult Run(params string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!File.Exists(AdbPath))
            throw new FileNotFoundException($"adb executable not found: {AdbPath}", AdbPath);

        var psi = new ProcessStartInfo(AdbPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start adb at '{AdbPath}'.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new AdbProcessResult(process.ExitCode, stdout, stderr);
    }
}
