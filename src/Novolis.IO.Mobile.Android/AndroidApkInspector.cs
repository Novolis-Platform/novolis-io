using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Novolis.IO.Mobile.Android;

/// <summary>
/// Inspects APK package, ABI, and signing metadata with Android SDK build tools.
/// </summary>
public static partial class AndroidApkInspector
{
    private static readonly Regex PackageRegex = new(
        @"\bpackage:\s+name='(?<name>[^']+)'(?:\s+versionCode='(?<code>\d+)')?(?:\s+versionName='(?<version>[^']*)')?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NativeCodeRegex = new(
        @"\bnative-code:\s*(?<values>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex QuotedValueRegex = new(
        @"'(?<value>[^']+)'",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SigningDigestRegex = new(
        @"(?im)\bcertificate\s+SHA-256\s+digest:\s*(?<digest>[0-9a-f: ]{8,})\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Runs the Android SDK tools without opening a console window.
    /// </summary>
    public static async Task<AndroidApkInspection> InspectAsync(
        string apkPath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ApkValidator.Validate(apkPath);
        if (!validation.Ok)
        {
            return new AndroidApkInspection
            {
                ApkPath = validation.ApkPath,
                Errors = validation.Errors,
                Warnings = validation.Warnings,
            };
        }

        var fullPath = validation.ApkPath;
        var tools = new List<string>();
        var warnings = new List<string>(validation.Warnings);
        var errors = new List<string>();
        var aapt = FindBuildTool("aapt2.exe", "aapt.exe");
        if (aapt is null)
        {
            return new AndroidApkInspection
            {
                ApkPath = fullPath,
                Errors = ["Android SDK build-tools aapt/aapt2 was not found."],
                Warnings = warnings,
            };
        }

        tools.Add(aapt);
        var badging = await RunAsync(
                aapt,
                ["dump", "badging", fullPath],
                timeout ?? TimeSpan.FromSeconds(30),
                cancellationToken)
            .ConfigureAwait(false);
        if (!badging.Ok)
        {
            return new AndroidApkInspection
            {
                ApkPath = fullPath,
                Tools = tools,
                Errors = [$"aapt failed: {badging.Diagnostic}"],
                Warnings = warnings,
            };
        }

        var packageMatch = PackageRegex.Match(badging.StdOut);
        if (!packageMatch.Success)
        {
            return new AndroidApkInspection
            {
                ApkPath = fullPath,
                Tools = tools,
                Errors = ["aapt did not report an Android package name."],
                Warnings = warnings,
            };
        }

        var signer = FindBuildTool("apksigner.jar");
        string? signingDigest = null;
        if (signer is null)
        {
            warnings.Add("Android SDK apksigner.jar was not found; signing identity was not inspected.");
        }
        else
        {
            var java = FindJava();
            if (java is null)
            {
                warnings.Add("Java was not found; signing identity was not inspected.");
            }
            else
            {
                tools.Add(signer);
                tools.Add(java);
                var signing = await RunAsync(
                        java,
                        ["-jar", signer, "verify", "--print-certs", fullPath],
                        timeout ?? TimeSpan.FromSeconds(30),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!signing.Ok)
                {
                    errors.Add($"apksigner failed: {signing.Diagnostic}");
                }
                else
                {
                    signingDigest = ParseSigningCertificateSha256(signing.StdOut);
                    if (signingDigest is null)
                        errors.Add("apksigner did not report a SHA-256 signing certificate digest.");
                }
            }
        }

        var nativeCode = NativeCodeRegex.Match(badging.StdOut);
        return new AndroidApkInspection
        {
            Ok = errors.Count == 0,
            ApkPath = fullPath,
            PackageName = packageMatch.Groups["name"].Value,
            VersionName = packageMatch.Groups["version"].Success
                ? packageMatch.Groups["version"].Value
                : null,
            VersionCode = long.TryParse(
                packageMatch.Groups["code"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var versionCode)
                ? versionCode
                : null,
            NativeAbis = nativeCode.Success
                ? QuotedValueRegex.Matches(nativeCode.Groups["values"].Value)
                    .Select(match => match.Groups["value"].Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [],
            SigningCertificateSha256 = signingDigest,
            Tools = tools,
            Errors = errors,
            Warnings = warnings,
        };
    }

    /// <summary>Normalizes a certificate digest to uppercase hexadecimal.</summary>
    public static string? NormalizeCertificateDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
            return null;
        var normalized = new string(digest.Where(Uri.IsHexDigit).ToArray());
        return normalized.Length == 0 ? null : normalized.ToUpperInvariant();
    }

    /// <summary>Parses the digest emitted by <c>apksigner --print-certs</c>.</summary>
    public static string? ParseSigningCertificateSha256(string? output)
    {
        var match = SigningDigestRegex.Match(output ?? string.Empty);
        return match.Success
            ? NormalizeCertificateDigest(match.Groups["digest"].Value)
            : null;
    }

    private static string? FindBuildTool(params string[] names)
    {
        foreach (var root in SdkRoots())
        {
            var buildTools = Path.Combine(root, "build-tools");
            if (!Directory.Exists(buildTools))
                continue;

            foreach (var version in Directory.EnumerateDirectories(buildTools)
                         .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var name in names)
                {
                    foreach (var candidate in new[]
                             {
                                 Path.Combine(version, name),
                                 Path.Combine(version, "lib", name),
                             })
                    {
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }
        }

        return null;
    }

    private static string? FindJava()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var java = Path.Combine(
                javaHome,
                "bin",
                OperatingSystem.IsWindows() ? "java.exe" : "java");
            if (File.Exists(java))
                return java;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var java = Path.Combine(
                    directory,
                    OperatingSystem.IsWindows() ? "java.exe" : "java");
                if (File.Exists(java))
                    return java;
            }
        }

        return null;
    }

    private static IEnumerable<string> SdkRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var environmentName in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var value = Environment.GetEnvironmentVariable(environmentName);
            if (!string.IsNullOrWhiteSpace(value)
                && seen.Add(Path.GetFullPath(value)))
            {
                yield return Path.GetFullPath(value);
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var fallback = Path.Combine(localAppData, "Android", "Sdk");
            if (Directory.Exists(fallback) && seen.Add(fallback))
                yield return fallback;
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return new ProcessResult(false, "", "The inspection process did not start.");
        }
        catch (Exception exception)
        {
            return new ProcessResult(false, "", exception.Message);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var error = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            var stdout = await output.ConfigureAwait(false);
            var stderr = await error.ConfigureAwait(false);
            return new ProcessResult(
                process.ExitCode == 0,
                stdout,
                string.IsNullOrWhiteSpace(stderr) ? stdout : stderr.Trim());
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have exited at the timeout boundary.
            }

            return new ProcessResult(
                false,
                "",
                cancellationToken.IsCancellationRequested
                    ? "APK inspection was cancelled."
                    : $"APK inspection timed out after {timeout}.");
        }
    }

    private sealed record ProcessResult(bool Ok, string StdOut, string Diagnostic);
}
