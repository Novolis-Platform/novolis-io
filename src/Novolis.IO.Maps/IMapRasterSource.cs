using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>Loads encoded raster tiles without coupling a view to a provider.</summary>
public interface IMapRasterSource
{
    /// <summary>Tile service description.</summary>
    XyzMapTemplate Template { get; }

    /// <summary>Loads a tile, returning <see langword="null" /> when unavailable.</summary>
    ValueTask<MapRasterTile?> GetTileAsync(
        MapTileKey key,
        CancellationToken cancellationToken = default);
}
