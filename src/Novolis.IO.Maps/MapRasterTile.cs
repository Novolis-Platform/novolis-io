using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>Encoded raster tile bytes returned by a map source.</summary>
public sealed record MapRasterTile(
    MapTileKey Key,
    byte[] PngBytes,
    bool IsStale = false);
