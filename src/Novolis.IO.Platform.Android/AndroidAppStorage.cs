using System.Diagnostics;

namespace Novolis.IO.Platform.Android;

/// <summary>
/// Default on-device directories for a Novolis Android app.
/// Prefers <c>Documents/Novolis/{product}</c> so the phone Files app and
/// <c>adb pull</c> see the same tree. Falls back to app-specific external files
/// when scoped storage will not grant a normal directory there.
/// </summary>
public static class AndroidAppStorage
{
#if ANDROID
    static readonly Lock Gate = new();
    static readonly Dictionary<string, AndroidAppLocations> Cache = new(StringComparer.Ordinal);
#endif

    /// <summary>Resolves and creates the default directories for <paramref name="productName"/>.</summary>
    public static AndroidAppLocations Open(string productName)
    {
#if ANDROID
        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android Application.Context is not available.");
        return Open(context, productName);
#else
        _ = productName;
        throw new PlatformNotSupportedException("AndroidAppStorage.Open requires net10.0-android.");
#endif
    }

    /// <summary>Product root from <see cref="Open(string)"/>.</summary>
    public static string DefaultRoot(string productName) => Open(productName).RootDirectory;

#if ANDROID
    /// <summary>Resolves and creates the default directories for <paramref name="context"/>.</summary>
    public static AndroidAppLocations Open(global::Android.Content.Context context, string productName)
    {
        ArgumentNullException.ThrowIfNull(context);
        var product = AndroidStorageLayout.SanitizeProductName(productName);
        lock (Gate)
        {
            if (Cache.TryGetValue(product, out var existing))
                return existing;

            var created = Resolve(context, product);
            Cache[product] = created;
            Debug.WriteLine($"Novolis Android storage ({created.Kind}): {created.RootDirectory}");
            Debug.WriteLine(AndroidStorageLayout.DescribeForAdb(created));
            return created;
        }
    }

    static AndroidAppLocations Resolve(global::Android.Content.Context context, string product)
    {
        var externalRoot = ExternalFilesRoot(context, product);
        var sharedRoot = TryClaimSharedDocuments(context, product);

        string root;
        AndroidStorageKind kind;
        if (sharedRoot is not null)
        {
            root = sharedRoot;
            kind = AndroidStorageKind.SharedDocuments;
        }
        else if (externalRoot is not null)
        {
            root = externalRoot;
            kind = AndroidStorageKind.AppExternal;
        }
        else
        {
            var files = context.FilesDir?.AbsolutePath
                ?? throw new InvalidOperationException("Context.FilesDir is null.");
            root = AndroidStorageLayout.UnderOrganization(files, product);
            kind = AndroidStorageKind.Internal;
        }

        var filesDir = context.FilesDir?.AbsolutePath;
        if (!string.IsNullOrWhiteSpace(filesDir))
        {
            AndroidStorageMigration.MoveLegacyTree(Path.Combine(filesDir, product), root);
            AndroidStorageMigration.MoveLegacyTree(
                AndroidStorageLayout.UnderOrganization(filesDir, product),
                root);
        }

        if (externalRoot is not null)
            AndroidStorageMigration.MoveLegacyTree(externalRoot, root);

        var workspace = AndroidStorageLayout.Workspace(root);
        Directory.CreateDirectory(workspace);
        var cache = CacheRoot(context, product);
        Directory.CreateDirectory(cache);
        return new AndroidAppLocations(
            product,
            kind,
            root,
            workspace,
            cache,
            context.PackageName);
    }

    static string? ExternalFilesRoot(global::Android.Content.Context context, string product)
    {
        if (!ExternalWritable())
            return null;

        var files = context.GetExternalFilesDir(null)?.AbsolutePath;
        return string.IsNullOrWhiteSpace(files)
            ? null
            : AndroidStorageLayout.UnderOrganization(files, product);
    }

    static string CacheRoot(global::Android.Content.Context context, string product)
    {
        if (ExternalWritable())
        {
            var cache = context.ExternalCacheDir?.AbsolutePath;
            if (!string.IsNullOrWhiteSpace(cache))
                return AndroidStorageLayout.UnderOrganization(cache, product);
        }

        var internalCache = context.CacheDir?.AbsolutePath
            ?? throw new InvalidOperationException("Context.CacheDir is null.");
        return AndroidStorageLayout.UnderOrganization(internalCache, product);
    }

    static bool ExternalWritable()
    {
        var state = global::Android.OS.Environment.ExternalStorageState;
        return string.Equals(state, global::Android.OS.Environment.MediaMounted, StringComparison.Ordinal);
    }

    static string? TryClaimSharedDocuments(global::Android.Content.Context context, string product)
    {
        try
        {
            var documents = PublicDocumentsDirectory();
            if (string.IsNullOrWhiteSpace(documents))
                return null;

            var root = AndroidStorageLayout.UnderOrganization(documents, product);
            if (TryProbeWrite(root))
                return root;

            var seeded = TrySeedWithMediaStore(context, product);
            var writable = seeded && TryProbeWrite(root);
            if (seeded)
                DeleteSeed(context, product);

            return writable ? root : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    static string? PublicDocumentsDirectory()
    {
#pragma warning disable CS0618
        var dir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
            global::Android.OS.Environment.DirectoryDocuments);
        var path = dir?.AbsolutePath;
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        var root = global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
#pragma warning restore CS0618
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, "Documents");
    }

    static bool TrySeedWithMediaStore(global::Android.Content.Context context, string product)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            return false;

        var resolver = context.ContentResolver;
        if (resolver is null)
            return false;

        global::Android.Net.Uri? uri = null;
        try
        {
            var values = new global::Android.Content.ContentValues();
            values.Put(global::Android.Provider.MediaStore.IMediaColumns.DisplayName, ".novolis-keep");
            values.Put(global::Android.Provider.MediaStore.IMediaColumns.MimeType, "application/octet-stream");
            values.Put(
                global::Android.Provider.MediaStore.IMediaColumns.RelativePath,
                AndroidStorageLayout.MediaStoreDocumentsRelative(product) + "/");
            var collection = global::Android.Provider.MediaStore.Files.GetContentUri("external");
            if (collection is null)
                return false;

            uri = resolver.Insert(collection, values);
            if (uri is null)
                return false;

            using var stream = resolver.OpenOutputStream(uri);
            stream?.WriteByte((byte)' ');
            stream?.Flush();
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            DeleteUri(resolver, uri);
            return false;
        }
    }

    static void DeleteSeed(global::Android.Content.Context context, string product)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            return;

        var resolver = context.ContentResolver;
        if (resolver is null)
            return;

        try
        {
            var collection = global::Android.Provider.MediaStore.Files.GetContentUri("external");
            if (collection is null)
                return;

            var displayName = global::Android.Provider.MediaStore.IMediaColumns.DisplayName;
            var relativePath = global::Android.Provider.MediaStore.IMediaColumns.RelativePath;
            var relative = AndroidStorageLayout.MediaStoreDocumentsRelative(product) + "/";
            var where = displayName + "=? AND " + relativePath + "=?";
            resolver.Delete(collection, where, [".novolis-keep", relative]);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    static void DeleteUri(global::Android.Content.ContentResolver resolver, global::Android.Net.Uri? uri)
    {
        if (uri is null)
            return;

        try
        {
            resolver.Delete(uri, null, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    static bool TryProbeWrite(string root)
    {
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, ".novolis-write-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or Java.IO.IOException
            or Java.Lang.SecurityException)
        {
            return false;
        }
    }
#endif
}
