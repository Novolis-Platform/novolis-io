namespace Novolis.IO.Maps;

/// <summary>A normalized tile key in the Web Mercator slippy-map scheme.</summary>
public readonly record struct MapTileKey
{
    /// <summary>Creates and normalizes a tile key.</summary>
    public MapTileKey(int zoom, int x, int y)
    {
        if (zoom < 0 || zoom > (int)WebMercatorTiles.MaximumZoom)
            throw new ArgumentOutOfRangeException(
                nameof(zoom),
                zoom,
                $"Tile zoom must be between 0 and {WebMercatorTiles.MaximumZoom}.");

        var tileCount = 1 << zoom;
        if (y < 0 || y >= tileCount)
            throw new ArgumentOutOfRangeException(
                nameof(y),
                y,
                "Tile Y must be within the world.");

        Zoom = zoom;
        X = Modulo(x, tileCount);
        Y = y;
    }

    /// <summary>Tile zoom.</summary>
    public int Zoom { get; }

    /// <summary>Normalized tile X.</summary>
    public int X { get; }

    /// <summary>Tile Y.</summary>
    public int Y { get; }

    static int Modulo(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
}
