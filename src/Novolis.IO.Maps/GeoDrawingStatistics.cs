namespace Novolis.IO.Maps;

/// <summary>Derived geometry measurements for a completed map drawing.</summary>
public readonly record struct GeoDrawingStatistics(
    int VertexCount,
    double LengthMeters,
    double AreaSquareMeters,
    double? RadiusMeters);
