using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>Describes a Web Mercator XYZ raster tile service.</summary>
public sealed record XyzMapTemplate
{
    /// <summary>Creates a tile service description.</summary>
    public XyzMapTemplate(
        string name,
        string urlTemplate,
        string attribution,
        MapTileAxisOrder axisOrder = MapTileAxisOrder.XThenY,
        IReadOnlyList<string>? subdomains = null,
        TimeSpan? minimumCacheAge = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(attribution);

        if (!urlTemplate.Contains("{z}", StringComparison.Ordinal)
            || !urlTemplate.Contains("{x}", StringComparison.Ordinal)
            || !urlTemplate.Contains("{y}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The URL template must contain {z}, {x}, and {y}.",
                nameof(urlTemplate));
        }

        if (!Uri.TryCreate(
                urlTemplate
                    .Replace("{z}", "0", StringComparison.Ordinal)
                    .Replace("{x}", "0", StringComparison.Ordinal)
                    .Replace("{y}", "0", StringComparison.Ordinal)
                    .Replace("{s}", "a", StringComparison.Ordinal),
                UriKind.Absolute,
                out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "The URL template must be an absolute HTTP(S) URL.",
                nameof(urlTemplate));
        }

        var resolvedSubdomains = subdomains?.ToArray() ?? [];
        if (urlTemplate.Contains("{s}", StringComparison.Ordinal)
            && resolvedSubdomains.Length == 0)
        {
            throw new ArgumentException(
                "A template containing {s} must provide subdomains.",
                nameof(subdomains));
        }

        if (resolvedSubdomains.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Subdomains cannot contain blank values.",
                nameof(subdomains));

        var resolvedMinimumCacheAge = minimumCacheAge ?? TimeSpan.Zero;
        if (resolvedMinimumCacheAge < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(minimumCacheAge),
                resolvedMinimumCacheAge,
                "The minimum cache age cannot be negative.");

        Name = name.Trim();
        UrlTemplate = urlTemplate.Trim();
        Attribution = attribution.Trim();
        AxisOrder = axisOrder;
        Subdomains = resolvedSubdomains;
        MinimumCacheAge = resolvedMinimumCacheAge;
    }

    /// <summary>Stable name used for cache partitioning.</summary>
    public string Name { get; }

    /// <summary>Absolute URL template containing {z}, {x}, and {y}.</summary>
    public string UrlTemplate { get; }

    /// <summary>Attribution that the host must show when tiles are visible.</summary>
    public string Attribution { get; }

    /// <summary>Provider's documented row and column convention.</summary>
    public MapTileAxisOrder AxisOrder { get; }

    /// <summary>Optional subdomains used to distribute tile requests.</summary>
    public IReadOnlyList<string> Subdomains { get; }

    /// <summary>Minimum time a cached tile is reused before refresh.</summary>
    public TimeSpan MinimumCacheAge { get; }

    /// <summary>Builds a request URI for a normalized tile key.</summary>
    public Uri BuildUri(MapTileKey key)
    {
        var subdomain = Subdomains.Count == 0
            ? string.Empty
            : Subdomains[Modulo(key.X + key.Y, Subdomains.Count)];
        var value = UrlTemplate
            .Replace("{z}", key.Zoom.ToString(
                global::System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace("{x}", key.X.ToString(
                global::System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace("{y}", key.Y.ToString(
                global::System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace("{s}", subdomain, StringComparison.Ordinal);

        return new Uri(value, UriKind.Absolute);
    }

    static int Modulo(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
}
