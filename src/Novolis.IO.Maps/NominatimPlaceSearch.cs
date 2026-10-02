using System.Globalization;
using System.Text.Json;
using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>OpenStreetMap Nominatim place search client.</summary>
public sealed class NominatimPlaceSearch : IMapPlaceSearch
{
    const string SearchEndpoint = "https://nominatim.openstreetmap.org/search";
    readonly HttpClient _httpClient;
    readonly TimeProvider _timeProvider;

    /// <summary>Creates a rate-limited search client over an HTTP client.</summary>
    public NominatimPlaceSearch(
        HttpClient httpClient,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Novolis.IO.Maps/1.0");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MapPlace>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        await MapRequestRateLimiterRegistry.WaitAsync(
            "nominatim-search",
            TimeSpan.FromSeconds(1),
            _timeProvider,
            cancellationToken);
        var uri =
            $"{SearchEndpoint}?format=jsonv2&limit=10"
            + $"&q={Uri.EscapeDataString(query.Trim())}";

        var json = await _httpClient.GetStringAsync(uri, cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<MapPlace>();
        foreach (var place in document.RootElement.EnumerateArray())
        {
            if (!place.TryGetProperty("display_name", out var label)
                || !TryGetDouble(place, "lat", out var latitude)
                || !TryGetDouble(place, "lon", out var longitude))
            {
                continue;
            }

            try
            {
                results.Add(new MapPlace(
                    label.GetString() ?? "Unnamed place",
                    new GeoCoordinate(latitude, longitude)));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Ignore malformed provider entries.
            }
        }

        return results;
    }

    static bool TryGetDouble(
        JsonElement element,
        string propertyName,
        out double value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.Number)
            return property.TryGetDouble(out value);

        return property.ValueKind == JsonValueKind.String
            && double.TryParse(
                property.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
    }
}
