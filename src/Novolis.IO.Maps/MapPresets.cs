namespace Novolis.IO.Maps;

/// <summary>Raster map templates that can be used without an API key.</summary>
public static class MapPresets
{
    const string KartverketTemplate =
        "https://cache.kartverket.no/v1/wmts/1.0.0/{0}/default/webmercator/{z}/{y}/{x}.png";
    static readonly TimeSpan SharedMinimumCacheAge = TimeSpan.FromDays(7);

    /// <summary>Kartverket color topographic map.</summary>
    public static XyzMapTemplate KartverketTopo { get; } = Kartverket(
        "kartverket-topo",
        "topo",
        "© Kartverket");

    /// <summary>Kartverket greyscale topographic map.</summary>
    public static XyzMapTemplate KartverketGreyscale { get; } = Kartverket(
        "kartverket-topograatone",
        "topograatone",
        "© Kartverket");

    /// <summary>Kartverket hiking raster map.</summary>
    public static XyzMapTemplate KartverketRaster { get; } = Kartverket(
        "kartverket-toporaster",
        "toporaster",
        "© Kartverket");

    /// <summary>Kartverket nautical raster map.</summary>
    public static XyzMapTemplate KartverketNautical { get; } = Kartverket(
        "kartverket-sjokartraster",
        "sjokartraster",
        "© Kartverket");

    /// <summary>OpenStreetMap standard raster map.</summary>
    public static XyzMapTemplate OpenStreetMapStandard { get; } = new(
        "openstreetmap-standard",
        "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
        "© OpenStreetMap contributors",
        minimumCacheAge: SharedMinimumCacheAge);

    /// <summary>OpenTopoMap raster map.</summary>
    public static XyzMapTemplate OpenTopoMap { get; } = new(
        "opentopomap",
        "https://tile.opentopomap.org/{z}/{x}/{y}.png",
        "© OpenStreetMap contributors, SRTM | © OpenTopoMap (CC-BY-SA)",
        minimumCacheAge: SharedMinimumCacheAge);

    /// <summary>CyclOSM cycling raster map.</summary>
    public static XyzMapTemplate CyclOSM { get; } = new(
        "cyclosm",
        "https://{s}.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png",
        "© OpenStreetMap contributors © CyclOSM",
        subdomains: ["a", "b", "c"],
        minimumCacheAge: SharedMinimumCacheAge);

    /// <summary>All keyless presets in the recommended display order.</summary>
    public static IReadOnlyList<XyzMapTemplate> All { get; } =
    [
        KartverketTopo,
        KartverketGreyscale,
        KartverketRaster,
        KartverketNautical,
        OpenStreetMapStandard,
        OpenTopoMap,
        CyclOSM,
    ];

    static XyzMapTemplate Kartverket(
        string name,
        string layer,
        string attribution) =>
        new(
            name,
            KartverketTemplate.Replace(
                "{0}",
                layer,
                StringComparison.Ordinal),
            attribution,
            MapTileAxisOrder.YThenX,
            minimumCacheAge: SharedMinimumCacheAge);
}
