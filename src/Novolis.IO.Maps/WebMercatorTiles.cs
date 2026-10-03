using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>Slippy-map framing operations for Web Mercator raster tiles.</summary>
public static class WebMercatorTiles
{
    /// <summary>Standard raster tile edge length in pixels.</summary>
    public const double TileSizePixels = 256;

    /// <summary>Lowest supported world zoom.</summary>
    public const double MinimumZoom = 0;

    /// <summary>Highest supported world zoom.</summary>
    public const double MaximumZoom = 22;

    /// <summary>Converts a geographic coordinate to viewport pixel coordinates.</summary>
    public static (double X, double Y) GeoToPixel(
        GeoCoordinate center,
        double zoom,
        double width,
        double height,
        GeoCoordinate coordinate)
    {
        ValidateViewport(zoom, width, height);

        var centerProjected = WebMercatorProjection.Project(center);
        var projected = WebMercatorProjection.Project(coordinate);
        var deltaX = projected.X - centerProjected.X;
        if (deltaX > 0.5)
            deltaX -= 1;
        else if (deltaX < -0.5)
            deltaX += 1;

        var worldPixels = WorldPixels(zoom);
        return (
            width / 2 + deltaX * worldPixels,
            height / 2 + (projected.Y - centerProjected.Y) * worldPixels);
    }

    /// <summary>
    /// Converts a connected geographic path to pixels while unwrapping each
    /// segment to its shortest antimeridian-relative representation.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> GeoPathToPixels(
        GeoCoordinate center,
        double zoom,
        double width,
        double height,
        IReadOnlyList<GeoCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        ValidateViewport(zoom, width, height);
        if (points.Count == 0)
            return [];

        var centerProjected = WebMercatorProjection.Project(center);
        var worldPixels = WorldPixels(zoom);
        var pixels = new (double X, double Y)[points.Count];
        var previousWorldX = double.NaN;
        for (var index = 0; index < points.Count; index++)
        {
            var projected = WebMercatorProjection.Project(points[index]);
            var worldX = projected.X;
            while (worldX - centerProjected.X > 0.5)
                worldX -= 1;
            while (worldX - centerProjected.X < -0.5)
                worldX += 1;

            if (double.IsFinite(previousWorldX))
            {
                while (worldX - previousWorldX > 0.5)
                    worldX -= 1;
                while (worldX - previousWorldX < -0.5)
                    worldX += 1;
            }

            previousWorldX = worldX;
            pixels[index] = (
                width / 2 + (worldX - centerProjected.X) * worldPixels,
                height / 2 + (projected.Y - centerProjected.Y) * worldPixels);
        }

        return pixels;
    }

    /// <summary>Converts viewport pixel coordinates to a geographic coordinate.</summary>
    public static GeoCoordinate PixelToGeo(
        GeoCoordinate center,
        double zoom,
        double width,
        double height,
        double x,
        double y)
    {
        ValidateViewport(zoom, width, height);

        var centerProjected = WebMercatorProjection.Project(center);
        var worldPixels = WorldPixels(zoom);
        var projectedX = WrapNormalized(
            centerProjected.X + (x - width / 2) / worldPixels);
        var projectedY = global::System.Math.Clamp(
            centerProjected.Y + (y - height / 2) / worldPixels,
            0d,
            1d);

        return WebMercatorProjection.Unproject(new GeoProjectedPoint(projectedX, projectedY));
    }

    /// <summary>Returns the tile keys that intersect a viewport.</summary>
    public static IReadOnlyList<MapTileKey> VisibleTiles(
        GeoCoordinate center,
        double zoom,
        double width,
        double height)
    {
        ValidateViewport(zoom, width, height);

        var tileZoom = global::System.Math.Clamp(
            (int)global::System.Math.Round(
                zoom,
                MidpointRounding.AwayFromZero),
            0,
            (int)MaximumZoom);
        var tileCount = 1 << tileZoom;
        var tileSize = TileSizePixels
            * global::System.Math.Pow(2, zoom - tileZoom);
        var centerProjected = WebMercatorProjection.Project(center);
        var centerWorldX = centerProjected.X * tileCount * tileSize;
        var centerWorldY = centerProjected.Y * tileCount * tileSize;
        var left = centerWorldX - width / 2;
        var right = centerWorldX + width / 2;
        var top = centerWorldY - height / 2;
        var bottom = centerWorldY + height / 2;
        var firstX = (int)global::System.Math.Floor(left / tileSize);
        var lastX = (int)global::System.Math.Floor(
            double.BitDecrement(right) / tileSize);
        var firstY = (int)global::System.Math.Floor(top / tileSize);
        var lastY = (int)global::System.Math.Floor(
            double.BitDecrement(bottom) / tileSize);
        var keys = new List<MapTileKey>();
        var seen = new HashSet<MapTileKey>();

        for (var x = firstX; x <= lastX; x++)
        {
            for (var y = firstY; y <= lastY; y++)
            {
                if (y >= 0 && y < tileCount)
                {
                    var key = new MapTileKey(tileZoom, x, y);
                    if (seen.Add(key))
                        keys.Add(key);
                }
            }
        }

        return keys.ToArray();
    }

    /// <summary>Returns the viewport pixel rectangle for a tile.</summary>
    public static (double X, double Y, double Width, double Height) TilePixelRect(
        GeoCoordinate center,
        double zoom,
        double width,
        double height,
        MapTileKey key)
    {
        ValidateViewport(zoom, width, height);

        var tileCount = 1 << key.Zoom;
        var tileSize = TileSizePixels
            * global::System.Math.Pow(2, zoom - key.Zoom);
        var worldSize = tileCount * tileSize;
        var centerProjected = WebMercatorProjection.Project(center);
        var centerWorldX = centerProjected.X * worldSize;
        var centerWorldY = centerProjected.Y * worldSize;
        var tileWorldX = key.X * tileSize;
        var deltaX = tileWorldX - centerWorldX;

        while (deltaX > worldSize / 2)
            deltaX -= worldSize;
        while (deltaX < -worldSize / 2)
            deltaX += worldSize;

        return (
            width / 2 + deltaX,
            height / 2 + key.Y * tileSize - centerWorldY,
            tileSize,
            tileSize);
    }

    /// <summary>
    /// Calculates a new center that keeps an anchor coordinate under a viewport pixel.
    /// </summary>
    public static GeoCoordinate CenterForAnchor(
        double zoom,
        double width,
        double height,
        GeoCoordinate anchor,
        double screenX,
        double screenY)
    {
        ValidateViewport(zoom, width, height);

        var projectedAnchor = WebMercatorProjection.Project(anchor);
        var worldPixels = WorldPixels(zoom);
        var centerX = WrapNormalized(
            projectedAnchor.X - (screenX - width / 2) / worldPixels);
        var centerY = global::System.Math.Clamp(
            projectedAnchor.Y - (screenY - height / 2) / worldPixels,
            0d,
            1d);

        return WebMercatorProjection.Unproject(
            new GeoProjectedPoint(centerX, centerY));
    }

    static double WorldPixels(double zoom) =>
        TileSizePixels * global::System.Math.Pow(2, zoom);

    static void ValidateViewport(double zoom, double width, double height)
    {
        if (!double.IsFinite(zoom) || zoom is < MinimumZoom or > MaximumZoom)
            throw new ArgumentOutOfRangeException(
                nameof(zoom),
                zoom,
                $"Zoom must be between {MinimumZoom} and {MaximumZoom}.");

        if (!double.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Width must be finite and greater than zero.");

        if (!double.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                "Height must be finite and greater than zero.");
    }

    static double WrapNormalized(double value)
    {
        var wrapped = value % 1d;
        return wrapped < 0 ? wrapped + 1d : wrapped;
    }
}
