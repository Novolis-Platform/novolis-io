using System.Globalization;
using System.Text.Json;
using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>OpenStreetMap Nominatim place search client.</summary>
public sealed class NominatimPlaceSearch : IMapPlaceSearch
{
    const string SearchEndpoint = "https://nominatim.openstreetmap.org/search";
    readonly HttpClient _httpClient;
    readonly SemaphoreSlim _requestGate = new(1, 1);
    DateTimeOffset _lastRequestStarted = DateTimeOffset.MinValue;

    /// <summary>Creates a rate-limited search client over an HTTP client.</summary>
    public NominatimPlaceSearch(HttpClient httpClient) =>
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <inheritdoc />
    public async Task<IReadOnlyList<MapPlace>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            var nextAllowed = _lastRequestStarted.AddSeconds(1);
            var delay = nextAllowed - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

            _lastRequestStarted = DateTimeOffset.UtcNow;
            var uri =
                $"{SearchEndpoint}?format=jsonv2&limit=10"
                + $"&q={Uri.EscapeDataString(query.Trim())}";

            string json;
            try
            {
                json = await _httpClient.GetStringAsync(uri, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return [];
            }

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
        finally
        {
            _requestGate.Release();
        }
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
