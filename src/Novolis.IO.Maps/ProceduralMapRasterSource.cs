using System.Buffers.Binary;
using System.IO.Compression;
using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>
/// Deterministic tile-addressed raster source for local rendering proofs such
/// as star fields. It performs no HTTP or disk-cache work.
/// </summary>
public sealed class ProceduralMapRasterSource : IMapRasterSource
{
    const int DefaultTileSize = 64;
    static readonly byte[] PngSignature =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
    ];

    readonly int _tileSize;

    /// <summary>Creates a deterministic procedural raster source.</summary>
    public ProceduralMapRasterSource(int tileSize = DefaultTileSize)
    {
        if (tileSize is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(tileSize));

        _tileSize = tileSize;
    }

    /// <inheritdoc />
    public XyzMapTemplate Template { get; } = new(
        "Procedural Starfield",
        "https://procedural.invalid/{z}/{x}/{y}.png",
        "Generated locally");

    /// <inheritdoc />
    public async ValueTask<MapRasterTile?> GetTileAsync(
        MapTileKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = await Task.Run(
            () => CreatePng(key, _tileSize, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return new MapRasterTile(key, bytes);
    }

    static byte[] CreatePng(
        MapTileKey key,
        int tileSize,
        CancellationToken cancellationToken)
    {
        using var raw = new MemoryStream(tileSize * tileSize * 4 + tileSize);
        for (var y = 0; y < tileSize; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            raw.WriteByte(0);
            for (var x = 0; x < tileSize; x++)
            {
                var hash = PixelHash(key, x, y);
                var star = hash % 97 == 0;
                raw.WriteByte(star ? (byte)220 : (byte)3);
                raw.WriteByte(star ? (byte)(120 + hash % 100) : (byte)12);
                raw.WriteByte(star ? (byte)255 : (byte)(30 + hash % 28));
                raw.WriteByte(255);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(
                   compressed,
                   CompressionLevel.Fastest,
                   leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        using var png = new MemoryStream();
        png.Write(PngSignature);
        WriteChunk(png, "IHDR"u8, CreateHeader(tileSize));
        WriteChunk(png, "IDAT"u8, compressed.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    static uint PixelHash(MapTileKey key, int x, int y)
    {
        var hash = 2166136261u;
        hash = Mix(hash, key.Zoom);
        hash = Mix(hash, key.X);
        hash = Mix(hash, key.Y);
        hash = Mix(hash, x);
        hash = Mix(hash, y);
        return hash;
    }

    static uint Mix(uint hash, int value)
    {
        hash ^= unchecked((uint)value);
        return hash * 16777619u;
    }

    static byte[] CreateHeader(int tileSize)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(
            header.AsSpan(0, sizeof(uint)),
            (uint)tileSize);
        BinaryPrimitives.WriteUInt32BigEndian(
            header.AsSpan(4, sizeof(uint)),
            (uint)tileSize);
        header[8] = 8;
        header[9] = 6;
        return header;
    }

    static void WriteChunk(
        Stream output,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);

        var crcInput = new byte[type.Length + data.Length];
        type.CopyTo(crcInput);
        data.CopyTo(crcInput.AsSpan(type.Length));
        Span<byte> crc = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(crcInput));
        output.Write(crc);
    }

    static uint Crc(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xEDB88320u & unchecked((uint)-(int)(crc & 1)));
        }

        return ~crc;
    }
}
