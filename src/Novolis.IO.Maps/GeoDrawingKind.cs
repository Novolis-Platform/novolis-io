namespace Novolis.IO.Maps;

/// <summary>Geographic drawing mode used by provider-neutral map interactions.</summary>
public enum GeoDrawingKind
{
    /// <summary>A single geographic point.</summary>
    Point,

    /// <summary>An open connected path, optionally measured for distance.</summary>
    Polyline,

    /// <summary>A closed connected path measured for perimeter and area.</summary>
    Polygon,

    /// <summary>A center and edge point represented as a geographic circle.</summary>
    Circle,

    /// <summary>Two opposite corners represented as a closed geographic rectangle.</summary>
    Rectangle,
}
