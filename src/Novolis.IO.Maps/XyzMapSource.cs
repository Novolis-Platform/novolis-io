using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>HTTP and bounded disk-cache implementation for an XYZ raster service.</summary>
public sealed class XyzMapSource : IMapRasterSource, IDisposable
{
    readonly HttpClient _httpClient;
    readonly string _cacheRoot;
    readonly string _rateLimiterKey;
    readonly XyzMapSourceOptions _options;
    readonly SemaphoreSlim _requestGate;
    readonly SemaphoreSlim _evictionGate = new(1, 1);
    readonly CancellationTokenSource _lifetime = new();
    readonly ConcurrentDictionary<
        MapTileKey,
        Lazy<Task<MapRasterTile?>>> _inflight = new();
    DateTimeOffset _lastEvictionAt = DateTimeOffset.MinValue;
    bool _disposed;

    /// <summary>Creates a source with a caller-owned cache directory.</summary>
    public XyzMapSource(
        HttpClient httpClient,
        XyzMapTemplate template,
        string cacheDirectory,
        string userAgent,
        XyzMapSourceOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        _options = options ?? new XyzMapSourceOptions();
        _options.Validate();
        _requestGate = new SemaphoreSlim(_options.MaximumConcurrentRequests);
        _rateLimiterKey = ProviderRateLimiterKey(template);

        var root = Path.GetFullPath(cacheDirectory);
        _cacheRoot = Path.Combine(root, CacheSegment(template));
        Directory.CreateDirectory(_cacheRoot);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        var path = CachePath(key);
        var fresh = await TryReadFreshAsync(path, cancellationToken);
        if (fresh is not null)
            return new MapRasterTile(key, fresh);

        var lazy = _inflight.GetOrAdd(
            key,
            static (tileKey, source) => new Lazy<Task<MapRasterTile?>>(
                () => source.LoadTileCoreAsync(tileKey),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this);
        var task = lazy.Value;
        try
        {
            return await task.WaitAsync(cancellationToken);
        }
        finally
        {
            if (task.IsCompleted
                && _inflight.TryGetValue(key, out var current)
                && ReferenceEquals(current, lazy))
            {
                _inflight.TryRemove(key, out _);
            }
        }
    }

    async Task<MapRasterTile?> LoadTileCoreAsync(MapTileKey key)
    {
        var path = CachePath(key);
        var stale = await TryReadStaleAsync(path, _lifetime.Token);

        try
        {
            await _requestGate.WaitAsync(_lifetime.Token);
            try
            {
                await MapRequestRateLimiterRegistry.WaitAsync(
                    _rateLimiterKey,
                    _options.MinimumRequestInterval,
                    _options.TimeProvider,
                    _lifetime.Token);
                using var response = await _httpClient.GetAsync(
                    Template.BuildUri(key),
                    HttpCompletionOption.ResponseHeadersRead,
                    _lifetime.Token);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength is > 0
                    && contentLength > _options.MaximumTileBytes)
                    throw new InvalidDataException("The map tile response is too large.");

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType is not null
                    && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"The map tile response has unexpected content type '{mediaType}'.");
                }

                var bytes = await ReadBoundedAsync(
                    response.Content,
                    _options.MaximumTileBytes,
                    _lifetime.Token);
                ValidatePng(bytes, _options.MaximumTileBytes);
                await WriteAtomicallyAsync(path, bytes, _lifetime.Token);
                try
                {
                    await MaybeEvictAsync();
                }
                catch (IOException)
                {
                    // A cache maintenance failure must not hide a valid tile.
                }
                catch (UnauthorizedAccessException)
                {
                    // A cache maintenance failure must not hide a valid tile.
                }
                return new MapRasterTile(key, bytes);
            }
            finally
            {
                _requestGate.Release();
            }
        }
        catch (HttpRequestException)
        {
            return stale is null ? null : new MapRasterTile(key, stale, IsStale: true);
        }
        catch (IOException)
        {
            return stale is null ? null : new MapRasterTile(key, stale, IsStale: true);
        }
        catch (InvalidDataException)
        {
            return stale is null ? null : new MapRasterTile(key, stale, IsStale: true);
        }
        catch (TaskCanceledException) when (!_lifetime.IsCancellationRequested)
        {
            return stale is null ? null : new MapRasterTile(key, stale, IsStale: true);
        }
    }

    string CachePath(MapTileKey key) =>
        Path.Combine(
            _cacheRoot,
            key.Zoom.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            key.X.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            $"{key.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture)}.png");

    async Task<byte[]?> TryReadFreshAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var age = FileAge(path, _options.TimeProvider);
        if (age is null
            || age < TimeSpan.Zero
            || age > Template.MinimumCacheAge)
        {
            return null;
        }

        return await TryReadValidBytesAsync(path, cancellationToken);
    }

    async Task<byte[]?> TryReadStaleAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var age = FileAge(path, _options.TimeProvider);
        if (age is null
            || age < TimeSpan.Zero
            || age > _options.MaximumStaleAge)
        {
            return null;
        }

        return await TryReadValidBytesAsync(path, cancellationToken);
    }

    async Task<byte[]?> TryReadValidBytesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > _options.MaximumTileBytes)
                return null;

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            ValidatePng(bytes, _options.MaximumTileBytes);
            return bytes;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    async Task WriteAtomicallyAsync(
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

    static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream(
            global::System.Math.Min(maximumBytes, 64 * 1024));
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        var total = 0;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(
                        0,
                        global::System.Math.Min(buffer.Length, maximumBytes - total + 1)),
                    cancellationToken);
                if (read == 0)
                    break;

                total += read;
                if (total > maximumBytes)
                    throw new InvalidDataException("The map tile response is too large.");

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    async Task MaybeEvictAsync()
    {
        if (_options.MinimumEvictionInterval > TimeSpan.Zero
            && _options.TimeProvider.GetUtcNow() - _lastEvictionAt
                < _options.MinimumEvictionInterval)
        {
            return;
        }

        await _evictionGate.WaitAsync(_lifetime.Token);
        try
        {
            var now = _options.TimeProvider.GetUtcNow();
            if (_options.MinimumEvictionInterval > TimeSpan.Zero
                && now - _lastEvictionAt < _options.MinimumEvictionInterval)
            {
                return;
            }

            _lastEvictionAt = now;
            var files = Directory
                .EnumerateFiles(_cacheRoot, "*.png", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderBy(file => file.LastWriteTimeUtc)
                .ToList();
            long totalBytes = files.Sum(file => file.Length);
            var removeCount = global::System.Math.Max(
                0,
                files.Count - _options.MaximumCachedTiles);
            for (var index = 0; index < files.Count; index++)
            {
                if (index < removeCount
                    || totalBytes > _options.MaximumCacheBytes)
                {
                    try
                    {
                        totalBytes -= files[index].Length;
                        files[index].Delete();
                    }
                    catch (IOException)
                    {
                        // Another process may have evicted the same tile.
                    }
                }
                else
                {
                    break;
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        finally
        {
            _evictionGate.Release();
        }
    }

    static TimeSpan? FileAge(string path, TimeProvider timeProvider)
    {
        if (!File.Exists(path))
            return null;

        var age = timeProvider.GetUtcNow() - File.GetLastWriteTimeUtc(path);
        return age;
    }

    static void ValidatePng(byte[] bytes, int maximumBytes)
    {
        if (bytes.Length < 8
            || bytes[0] != 0x89
            || bytes[1] != 0x50
            || bytes[2] != 0x4E
            || bytes[3] != 0x47
            || bytes[4] != 0x0D
            || bytes[5] != 0x0A
            || bytes[6] != 0x1A
            || bytes[7] != 0x0A)
        {
            throw new InvalidDataException("The map tile response is not a PNG image.");
        }

        if (bytes.Length > maximumBytes)
            throw new InvalidDataException("The map tile response is too large.");

        var offset = 8;
        var hasHeader = false;
        var hasEnd = false;
        while (offset + 12 <= bytes.Length)
        {
            var chunkLength = BinaryPrimitives.ReadUInt32BigEndian(
                bytes.AsSpan(offset, sizeof(uint)));
            if (chunkLength > int.MaxValue)
                throw new InvalidDataException("The map tile PNG chunk is too large.");

            var chunkEnd = offset + 12L + chunkLength;
            if (chunkEnd > bytes.Length)
                throw new InvalidDataException("The map tile PNG is truncated.");

            var chunkType = bytes.AsSpan(offset + 4, 4);
            if (!hasHeader)
            {
                if (!chunkType.SequenceEqual("IHDR"u8) || chunkLength != 13)
                    throw new InvalidDataException("The map tile PNG has no valid IHDR chunk.");

                var width = BinaryPrimitives.ReadUInt32BigEndian(
                    bytes.AsSpan(offset + 8, sizeof(uint)));
                var height = BinaryPrimitives.ReadUInt32BigEndian(
                    bytes.AsSpan(offset + 12, sizeof(uint)));
                if (width == 0 || height == 0)
                    throw new InvalidDataException("The map tile PNG has an empty image size.");

                hasHeader = true;
            }

            offset = (int)chunkEnd;
            if (chunkType.SequenceEqual("IEND"u8))
            {
                if (chunkLength != 0)
                    throw new InvalidDataException("The map tile PNG has an invalid IEND chunk.");

                hasEnd = true;
                break;
            }
        }

        if (!hasHeader || !hasEnd)
            throw new InvalidDataException("The map tile PNG has no complete image data.");
    }

    static string CacheSegment(XyzMapTemplate template)
    {
        var identity = string.Join(
            "\n",
            template.Name,
            template.UrlTemplate,
            template.AxisOrder,
            string.Join(",", template.Subdomains));
        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant()[..16];
        var name = new string(
            template.Name
                .Trim()
                .Select(character =>
                    char.IsLetterOrDigit(character) || character is '-' or '_'
                        ? character
                        : '-')
                .ToArray());
        return $"{(name.Length == 0 ? "map" : name)}-{hash}";
    }

    static string ProviderRateLimiterKey(XyzMapTemplate template)
    {
        var value = template.UrlTemplate
            .Replace("{z}", "0", StringComparison.Ordinal)
            .Replace("{x}", "0", StringComparison.Ordinal)
            .Replace("{y}", "0", StringComparison.Ordinal)
            .Replace("{s}", "subdomain", StringComparison.Ordinal);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? $"tiles:{uri.Host}"
            : $"tiles:{template.Name}";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _lifetime.Cancel();
        _requestGate.Dispose();
        _evictionGate.Dispose();
        _lifetime.Dispose();
    }
}
