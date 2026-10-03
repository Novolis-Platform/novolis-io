namespace Novolis.IO.Maps;

/// <summary>Provider-neutral family of a host-owned map overlay.</summary>
public enum MapOverlayKind
{
    /// <summary>A point of interest or location marker.</summary>
    Marker,

    /// <summary>A circular geographic area.</summary>
    Circle,

    /// <summary>An open connected geographic path.</summary>
    Track,

    /// <summary>A closed geographic area.</summary>
    Polygon,
}
