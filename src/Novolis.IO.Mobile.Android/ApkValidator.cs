using System.IO.Compression;

namespace Novolis.IO.Mobile.Android;

/// <summary>Validates a local APK before install (existence, size, zip structure).</summary>
public static class ApkValidator
{
    /// <summary>Validates <paramref name="apkPath"/> using <paramref name="options"/> constraints.</summary>
    public static ApkValidationResult Validate(string apkPath, ApkInstallOptions? options = null)
    {
        options ??= new ApkInstallOptions();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(apkPath))
            return ApkValidationResult.Fail("", 0, "APK path is required.");

        string full;
        try
        {
            full = Path.GetFullPath(apkPath);
        }
        catch (Exception ex)
        {
            return ApkValidationResult.Fail(apkPath, 0, $"Invalid APK path: {ex.Message}");
        }

        if (!File.Exists(full))
            return ApkValidationResult.Fail(full, 0, $"APK not found: {full}");

        var ext = Path.GetExtension(full);
        if (!ext.Equals(".apk", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".apks", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".xapk", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Unexpected extension '{ext}' (expected .apk).");
        }

        long size;
        try
        {
            size = new FileInfo(full).Length;
        }
        catch (Exception ex)
        {
            return ApkValidationResult.Fail(full, 0, $"Cannot read APK size: {ex.Message}");
        }

        if (size < options.MinApkBytes)
            return ApkValidationResult.Fail(full, size, $"APK too small ({size} bytes; min {options.MinApkBytes}).");

        if (options.MaxApkBytes is long max && size > max)
            return ApkValidationResult.Fail(full, size, $"APK too large ({size} bytes; max {max}).");

        if (options.RequireApkEntries)
        {
            try
            {
                using var zip = ZipFile.OpenRead(full);
                var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!names.Contains("AndroidManifest.xml"))
                    return ApkValidationResult.Fail(full, size, "APK is not a valid package zip (missing AndroidManifest.xml).");
                if (!names.Any(n => n.Equals("classes.dex", StringComparison.OrdinalIgnoreCase)
                                    || n.StartsWith("classes", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".dex", StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add("No classes*.dex entry found (unusual for a release APK).");
                }
            }
            catch (InvalidDataException)
            {
                return ApkValidationResult.Fail(full, size, "APK is not a readable zip archive.");
            }
            catch (Exception ex)
            {
                return ApkValidationResult.Fail(full, size, $"APK zip check failed: {ex.Message}");
            }
        }

        return ApkValidationResult.Success(full, size, warnings);
    }
}
