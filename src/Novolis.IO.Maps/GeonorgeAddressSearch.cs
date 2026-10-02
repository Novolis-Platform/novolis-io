using System.Text.Json;
using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>Geonorge address search client for Norwegian addresses.</summary>
public sealed class GeonorgeAddressSearch : IMapPlaceSearch
{
    const string SearchEndpoint = "https://ws.geonorge.no/adresser/v1/sok";
    readonly HttpClient _httpClient;

    /// <summary>Creates a search client over a caller-owned HTTP client.</summary>
    public GeonorgeAddressSearch(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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
            "geonorge-address",
            TimeSpan.FromMilliseconds(250),
            cancellationToken);
        var uri =
            $"{SearchEndpoint}?sok={Uri.EscapeDataString(query.Trim())}"
            + "&treffPerSide=10&side=0";

        var json = await _httpClient.GetStringAsync(uri, cancellationToken);

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("adresser", out var addresses)
            || addresses.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<MapPlace>();
        foreach (var address in addresses.EnumerateArray())
        {
            if (!address.TryGetProperty("adressetekst", out var label)
                || !address.TryGetProperty("representasjonspunkt", out var point)
                || !point.TryGetProperty("lat", out var latitude)
                || !point.TryGetProperty("lon", out var longitude)
                || !latitude.TryGetDouble(out var lat)
                || !longitude.TryGetDouble(out var lon))
            {
                continue;
            }

            try
            {
                results.Add(new MapPlace(
                    label.GetString() ?? "Unnamed address",
                    new GeoCoordinate(lat, lon)));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Ignore malformed provider entries.
            }
        }

        return results;
    }
}
