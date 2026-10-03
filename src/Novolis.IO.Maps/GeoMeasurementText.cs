using System.Globalization;
using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>Compact, provider-neutral text for interactive geographic measurements.</summary>
public static class GeoMeasurementText
{
    /// <summary>Formats a distance using metres or kilometres.</summary>
    public static string FormatDistance(double meters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(meters);
        if (!double.IsFinite(meters))
            throw new ArgumentOutOfRangeException(nameof(meters));

        return meters >= 1_000
            ? $"{(meters / 1_000d).ToString("0.##", CultureInfo.InvariantCulture)} km"
            : $"{meters.ToString("0.##", CultureInfo.InvariantCulture)} m";
    }

    /// <summary>Formats an area using square metres or square kilometres.</summary>
    public static string FormatArea(double squareMeters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(squareMeters);
        if (!double.IsFinite(squareMeters))
            throw new ArgumentOutOfRangeException(nameof(squareMeters));

        return squareMeters >= 1_000_000
            ? $"{(squareMeters / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture)} km²"
            : $"{squareMeters.ToString("0.##", CultureInfo.InvariantCulture)} m²";
    }

    /// <summary>Describes the current, possibly incomplete, drawing preview.</summary>
    public static string ForDrawing(
        GeoDrawingKind kind,
        IReadOnlyList<GeoCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        return kind switch
        {
            GeoDrawingKind.Point => "Point · click to place",
            GeoDrawingKind.Circle => FormatCircle(points),
            GeoDrawingKind.Polyline => FormatPolyline(points),
            GeoDrawingKind.Polygon => FormatPolygon(points),
            GeoDrawingKind.Rectangle => FormatRectangle(points),
            _ => string.Empty,
        };
    }

    static string FormatCircle(IReadOnlyList<GeoCoordinate> points)
    {
        if (points.Count < 2)
            return "Circle · drag from center to set radius";

        var radius = GeoDistance.Between(points[0], points[1]);
        return $"Circle · radius {FormatDistance(radius)} · area {FormatArea(
            global::System.Math.PI * radius * radius)}";
    }

    static string FormatPolyline(IReadOnlyList<GeoCoordinate> points) =>
        points.Count < 2
            ? "Polyline · click to add points"
            : $"Polyline · {points.Count} points · {FormatDistance(
                GeoPathMetrics.PolylineLength(points))}";

    static string FormatPolygon(IReadOnlyList<GeoCoordinate> points) =>
        points.Count < 3
            ? $"Polygon · {points.Count}/3 points · click to add points"
            : $"Polygon · {points.Count} points · perimeter {FormatDistance(
                GeoPathMetrics.PolylineLength(points, close: true))} · area {FormatArea(
                GeoPathMetrics.PolygonAreaSquareMeters(points))}";

    static string FormatRectangle(IReadOnlyList<GeoCoordinate> points)
    {
        if (points.Count < 2)
            return "Rectangle · drag to set opposite corner";

        var rectangle = new GeoRectangle(points[0], points[1]);
        return $"Rectangle · perimeter {FormatDistance(rectangle.PerimeterMeters)} · area {FormatArea(
            rectangle.AreaSquareMeters)}";
    }
}
