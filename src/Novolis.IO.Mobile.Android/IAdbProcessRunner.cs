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
    public bool Ok => ExitCode == 0 && !TimedOut && !Cancelled;

    /// <summary>Whether the process exceeded its timeout.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Whether the caller cancelled the process.</summary>
    public bool Cancelled { get; init; }

    /// <summary>Whether captured output was truncated.</summary>
    public bool Truncated { get; init; }

    /// <summary>Combined diagnostic text (stderr preferred when non-empty).</summary>
    public string Diagnostic =>
        string.IsNullOrWhiteSpace(StdErr) ? StdOut.Trim() : StdErr.Trim();
}

/// <summary>Result of a process whose standard output may contain binary data.</summary>
public sealed record AdbBinaryProcessResult(int ExitCode, byte[] StdOut, string StdErr)
{
    /// <summary>Whether the process completed successfully.</summary>
    public bool Ok => ExitCode == 0 && !TimedOut && !Cancelled;

    /// <summary>Whether the process exceeded its timeout.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Whether the caller cancelled the process.</summary>
    public bool Cancelled { get; init; }

    /// <summary>Combined diagnostic text.</summary>
    public string Diagnostic => string.IsNullOrWhiteSpace(StdErr) ? "" : StdErr.Trim();
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

    /// <summary>
    /// Executes <c>adb</c> asynchronously with a cancellation and timeout boundary.
    /// Existing implementations receive a safe compatibility implementation.
    /// </summary>
    Task<AdbProcessResult> RunAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        params string[] args) =>
        Task.Run(() => Run(args), cancellationToken).WaitAsync(timeout, cancellationToken);

    /// <summary>
    /// Executes <c>adb</c> while preserving binary standard output.
    /// Existing implementations receive a UTF-8 compatibility implementation.
    /// </summary>
    async Task<AdbBinaryProcessResult> RunBinaryAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        params string[] args)
    {
        var result = await RunAsync(timeout, cancellationToken, args).ConfigureAwait(false);
        return new AdbBinaryProcessResult(
            result.ExitCode,
            Encoding.UTF8.GetBytes(result.StdOut),
            result.StdErr)
        {
            TimedOut = result.TimedOut,
            Cancelled = result.Cancelled,
        };
    }
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

    /// <inheritdoc />
    public async Task<AdbProcessResult> RunAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        params string[] args)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        ArgumentNullException.ThrowIfNull(args);

        using var process = Start(args);
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return new AdbProcessResult(process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new AdbProcessResult(1, "", "adb operation cancelled.")
            {
                Cancelled = true,
            };
        }
        catch (TimeoutException)
        {
            TryKill(process);
            return new AdbProcessResult(1, "", $"adb operation timed out after {timeout}.")
            {
                TimedOut = true,
            };
        }
    }

    /// <inheritdoc />
    public async Task<AdbBinaryProcessResult> RunBinaryAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        params string[] args)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        ArgumentNullException.ThrowIfNull(args);

        using var process = Start(args);
        using var output = new MemoryStream();
        try
        {
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            await copyTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return new AdbBinaryProcessResult(process.ExitCode, output.ToArray(), stderr);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new AdbBinaryProcessResult(1, [], "adb operation cancelled.")
            {
                Cancelled = true,
            };
        }
        catch (TimeoutException)
        {
            TryKill(process);
            return new AdbBinaryProcessResult(1, [], $"adb operation timed out after {timeout}.")
            {
                TimedOut = true,
            };
        }
    }

    private Process Start(string[] args)
    {
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

        return Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start adb at '{AdbPath}'.");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited between HasExited and Kill.
        }
    }
}
