using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>A completed provider-neutral geographic map drawing.</summary>
public sealed record GeoDrawing
{
    /// <summary>Creates a geographic drawing from its vertices.</summary>
    public GeoDrawing(
        GeoDrawingKind kind,
        IReadOnlyList<GeoCoordinate> points,
        double? radiusMeters = null)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
            throw new ArgumentException("A drawing requires at least one point.", nameof(points));

        if (!HasEnoughPoints(kind, points.Count))
            throw new ArgumentException(
                $"A {kind} drawing has insufficient points.",
                nameof(points));

        if (kind == GeoDrawingKind.Circle)
        {
            if (radiusMeters is null)
                radiusMeters = GeoDistance.Between(points[0], points[1]);
        }
        else if (radiusMeters is not null)
        {
            throw new ArgumentException(
                "Only circle drawings may specify a radius.",
                nameof(radiusMeters));
        }

        if (radiusMeters is { } radius
            && (!double.IsFinite(radius) || radius < 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMeters),
                radius,
                "Radius must be finite and non-negative.");
        }

        Kind = kind;
        Points = points.ToArray();
        RadiusMeters = radiusMeters;
    }

    /// <summary>Drawing geometry kind.</summary>
    public GeoDrawingKind Kind { get; }

    /// <summary>Geographic vertices in drawing order.</summary>
    public IReadOnlyList<GeoCoordinate> Points { get; }

    /// <summary>Circle radius, or null for non-circle drawings.</summary>
    public double? RadiusMeters { get; }

    /// <summary>Rectangle geometry, or null for non-rectangle drawings.</summary>
    public GeoRectangle? Rectangle =>
        Kind == GeoDrawingKind.Rectangle
            ? new GeoRectangle(Points[0], Points[1])
            : null;

    /// <summary>Connected drawing vertices, repeating the first vertex when closed.</summary>
    public IReadOnlyList<GeoCoordinate> ClosedPoints =>
        Kind switch
        {
            GeoDrawingKind.Polygon when Points[0] != Points[^1] => [.. Points, Points[0]],
            GeoDrawingKind.Rectangle => Rectangle!.Value.ClosedCorners,
            _ => Points,
        };

    /// <summary>Great-circle path length in meters.</summary>
    public double LengthMeters =>
        Kind switch
        {
            GeoDrawingKind.Circle => 2 * global::System.Math.PI * RadiusMeters.GetValueOrDefault(),
            GeoDrawingKind.Polygon => GeoPathMetrics.PolylineLength(Points, close: true),
            GeoDrawingKind.Rectangle => Rectangle!.Value.PerimeterMeters,
            _ => GeoPathMetrics.PolylineLength(Points),
        };

    /// <summary>Approximate surface area in square meters for a polygon or circle.</summary>
    public double AreaSquareMeters =>
        Kind switch
        {
            GeoDrawingKind.Circle => global::System.Math.PI
                * global::System.Math.Pow(RadiusMeters.GetValueOrDefault(), 2),
            GeoDrawingKind.Polygon => GeoPathMetrics.PolygonAreaSquareMeters(Points),
            GeoDrawingKind.Rectangle => Rectangle!.Value.AreaSquareMeters,
            _ => 0,
        };

    /// <summary>Derived geometry measurements for list and inspector surfaces.</summary>
    public GeoDrawingStatistics Statistics => new(
        Kind == GeoDrawingKind.Rectangle ? 4 : Points.Count,
        LengthMeters,
        AreaSquareMeters,
        RadiusMeters);

    static bool HasEnoughPoints(GeoDrawingKind kind, int count) =>
        kind switch
        {
            GeoDrawingKind.Point => count >= 1,
            GeoDrawingKind.Polyline => count >= 2,
            GeoDrawingKind.Polygon => count >= 3,
            GeoDrawingKind.Circle => count >= 2,
            GeoDrawingKind.Rectangle => count == 2,
            _ => false,
        };
}
