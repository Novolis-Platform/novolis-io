namespace Novolis.IO.Maps;

/// <summary>
/// A provider-neutral 2D point for a projected scene, including non-terrestrial
/// sources such as star catalogs, diagrams, or simulation fields.
/// </summary>
public sealed record ProjectedScenePoint
{
    /// <summary>Creates a finite, selectable scene point.</summary>
    public ProjectedScenePoint(
        string id,
        double x,
        double y,
        string? label = null,
        double? radiusPixels = null,
        double? magnitude = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        object? tag = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A scene point id is required.", nameof(id));
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y));
        if (radiusPixels is { } radius
            && (!double.IsFinite(radius) || radius <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(radiusPixels));
        }
        if (magnitude is { } sourceMagnitude && !double.IsFinite(sourceMagnitude))
            throw new ArgumentOutOfRangeException(nameof(magnitude));

        Id = id.Trim();
        X = x;
        Y = y;
        Label = label;
        RadiusPixels = radiusPixels;
        Magnitude = magnitude;
        Metadata = metadata;
        Tag = tag;
    }

    /// <summary>Stable source-owned identity.</summary>
    public string Id { get; }

    /// <summary>Projected horizontal coordinate in source-defined units.</summary>
    public double X { get; }

    /// <summary>Projected vertical coordinate in source-defined units.</summary>
    public double Y { get; }

    /// <summary>Optional display label.</summary>
    public string? Label { get; }

    /// <summary>Optional rendered point radius in screen pixels.</summary>
    public double? RadiusPixels { get; }

    /// <summary>Optional source magnitude or weight exposed to scene renderers.</summary>
    public double? Magnitude { get; }

    /// <summary>Optional displayable source metadata.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    /// <summary>Optional host-owned source payload.</summary>
    public object? Tag { get; }
}
