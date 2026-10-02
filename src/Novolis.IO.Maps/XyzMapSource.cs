using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>HTTP and disk-cache implementation for an XYZ raster service.</summary>
public sealed class XyzMapSource : IMapRasterSource
{
    readonly HttpClient _httpClient;
    readonly string _cacheDirectory;

    /// <summary>Creates a source with a caller-owned cache directory.</summary>
    public XyzMapSource(
        HttpClient httpClient,
        XyzMapTemplate template,
        string cacheDirectory,
        string userAgent)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        _cacheDirectory = Path.GetFullPath(cacheDirectory);
        Directory.CreateDirectory(_cacheDirectory);
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent.Trim());
    }

    /// <inheritdoc />
    public XyzMapTemplate Template { get; }

    /// <inheritdoc />
    public async ValueTask<MapRasterTile?> GetTileAsync(
        MapTileKey key,
        CancellationToken cancellationToken = default)
    {
        var path = CachePath(key);
        if (File.Exists(path))
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
            if (age < Template.MinimumCacheAge)
            {
                var cached = await TryReadAsync(path, cancellationToken);
                if (cached is not null)
                    return new MapRasterTile(key, cached);
            }
        }

        byte[]? stale = await TryReadAsync(path, cancellationToken);
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(
                Template.BuildUri(key),
                cancellationToken);
            await WriteAtomicallyAsync(path, bytes, cancellationToken);
            return new MapRasterTile(key, bytes);
        }
        catch (HttpRequestException)
        {
            return stale is null ? null : new MapRasterTile(key, stale);
        }
        catch (IOException)
        {
            return stale is null ? null : new MapRasterTile(key, stale);
        }
    }

    string CachePath(MapTileKey key) =>
        Path.Combine(
            _cacheDirectory,
            CacheSegment(Template.Name),
            key.Zoom.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            key.X.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            $"{key.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture)}.png");

    static async Task<byte[]?> TryReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    static async Task WriteAtomicallyAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The tile cache path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // A successful cache write remains valid when cleanup races another writer.
            }
        }
    }

    static string CacheSegment(string value)
    {
        var characters = value
            .Trim()
            .Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_'
                    ? character
                    : '-')
            .ToArray();
        return characters.Length == 0 ? "map" : new string(characters);
    }
}
