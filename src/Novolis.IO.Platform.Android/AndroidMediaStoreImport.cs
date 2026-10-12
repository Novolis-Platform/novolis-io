namespace Novolis.IO.Platform.Android;

/// <summary>Copies a legacy Documents product tree into app-owned storage.</summary>
public static class AndroidMediaStoreImport
{
#if ANDROID
    /// <summary>
    /// Copies readable legacy files and the app's MediaStore-owned files into
    /// <paramref name="destinationRoot"/> without overwriting existing data.
    /// </summary>
    public static AndroidStorageImportResult Import(
        global::Android.Content.Context context,
        string productName,
        string destinationRoot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);

        var copied = 0;
        var errors = new List<string>();
        try
        {
            Directory.CreateDirectory(destinationRoot);
            var legacyRoot = TryLegacyRoot(productName);
            if (legacyRoot is not null)
                copied += CopyReadableTree(legacyRoot, destinationRoot, errors);

            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                return Result(copied, errors);

            var resolver = context.ContentResolver;
            var collection = resolver is null
                ? null
                : global::Android.Provider.MediaStore.Files.GetContentUri("external");
            if (resolver is null || collection is null)
            {
                errors.Add("Android MediaStore is unavailable.");
                return Result(copied, errors);
            }

            var relativeRoot =
                AndroidStorageLayout.MediaStoreDocumentsRelative(productName);
            var relativePathColumn =
                global::Android.Provider.MediaStore.IMediaColumns.RelativePath;
            var escapedRelativeRoot = relativeRoot.Replace("'", "''");
            using var cursor = resolver.Query(
                collection,
                ["_id", "_display_name", relativePathColumn],
                $"{relativePathColumn} LIKE '{escapedRelativeRoot}/%'",
                null,
                null);
            if (cursor is null)
            {
                errors.Add("Android MediaStore returned no cursor.");
                return Result(copied, errors);
            }

            var idColumn = cursor.GetColumnIndexOrThrow("_id");
            var displayNameColumn = cursor.GetColumnIndexOrThrow("_display_name");
            var relativePathIndex = cursor.GetColumnIndexOrThrow(relativePathColumn);
            var prefix = relativeRoot + "/";
            while (cursor.MoveToNext())
            {
                var id = cursor.GetLong(idColumn);
                var displayName = cursor.GetString(displayNameColumn);
                var relativePath = cursor.GetString(relativePathIndex);
                if (string.IsNullOrWhiteSpace(displayName)
                    || string.IsNullOrWhiteSpace(relativePath)
                    || !relativePath.StartsWith(prefix, StringComparison.Ordinal)
                    || (!displayName.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(
                            displayName,
                            "presence-report.txt",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var relativeDirectory = relativePath[prefix.Length..]
                    .TrimEnd('/');
                var relativeFile = string.IsNullOrEmpty(relativeDirectory)
                    ? displayName
                    : Path.Combine(relativeDirectory, displayName);
                if (!TryDestinationPath(destinationRoot, relativeFile, out var target))
                    continue;

                var source = global::Android.Content.ContentUris.WithAppendedId(
                    collection,
                    id);
                try
                {
                    if (CopyIfMissing(resolver, source, target))
                        copied++;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    errors.Add($"{relativeFile}: {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            errors.Add(exception.Message);
        }

        return Result(copied, errors);
    }

    static AndroidStorageImportResult Result(
        int copied,
        IReadOnlyCollection<string> errors) =>
        new(
            copied,
            errors.Count == 0
                ? null
                : string.Join(" | ", errors));

    static int CopyReadableTree(
        string sourceRoot,
        string destinationRoot,
        ICollection<string> errors)
    {
        var copied = 0;
        try
        {
            foreach (var source in Directory.EnumerateFiles(
                         sourceRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, source);
                if (!TryDestinationPath(destinationRoot, relative, out var target))
                    continue;

                try
                {
                    if (CopyIfMissing(source, target))
                        copied++;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    errors.Add($"{relative}: {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            errors.Add($"{sourceRoot}: {exception.Message}");
        }

        return copied;
    }

    static bool CopyIfMissing(string source, string target)
    {
        if (File.Exists(target))
            return false;

        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        try
        {
            File.Copy(source, target, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(target))
        {
            return false;
        }
    }

    static bool CopyIfMissing(
        global::Android.Content.ContentResolver resolver,
        global::Android.Net.Uri source,
        string target)
    {
        if (File.Exists(target))
            return false;

        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        using var input = resolver.OpenInputStream(source)
            ?? throw new InvalidOperationException("Android could not open a legacy ledger file.");
        using var output = new FileStream(
            target,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.SequentialScan,
            });
        input.CopyTo(output);
        return true;
    }

    static bool TryDestinationPath(
        string destinationRoot,
        string relative,
        out string target)
    {
        target = string.Empty;
        if (string.IsNullOrWhiteSpace(relative)
            || Path.IsPathRooted(relative))
        {
            return false;
        }

        var fullRoot = Path.GetFullPath(destinationRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(destinationRoot, relative));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        target = candidate;
        return true;
    }

    static string? TryLegacyRoot(string productName)
    {
#pragma warning disable CS0618
        var documents = global::Android.OS.Environment
            .GetExternalStoragePublicDirectory(
                global::Android.OS.Environment.DirectoryDocuments);
#pragma warning restore CS0618
        var path = documents?.AbsolutePath;
        return string.IsNullOrWhiteSpace(path)
            ? null
            : AndroidStorageLayout.UnderOrganization(path, productName);
    }
#endif
}
