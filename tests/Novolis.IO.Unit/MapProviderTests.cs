using System.Net;
using System.Text;
using Novolis.IO.Maps;
using Novolis.Math.Geometry;

namespace Novolis.IO.Unit;

public sealed class MapProviderTests
{
    [Test]
    public async Task Presets_build_the_documented_tile_urls()
    {
        var key = new MapTileKey(3, 4, 5);

        await Assert.That(MapPresets.KartverketTopo.BuildUri(key).ToString())
            .IsEqualTo(
                "https://cache.kartverket.no/v1/wmts/1.0.0/topo/default/webmercator/3/5/4.png");
        await Assert.That(MapPresets.OpenStreetMapStandard.BuildUri(key).ToString())
            .IsEqualTo("https://tile.openstreetmap.org/3/4/5.png");
        await Assert.That(MapPresets.CyclOSM.BuildUri(key).Host)
            .IsEqualTo("a.tile-cyclosm.openstreetmap.fr");
        await Assert.That(MapPresets.All).Count().IsEqualTo(7);
    }

    [Test]
    public async Task XyzMapSource_uses_a_fresh_cache_without_second_request()
    {
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-test-");
        try
        {
            var source = new XyzMapSource(
                client,
                new XyzMapTemplate(
                    "test-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test",
                    minimumCacheAge: TimeSpan.FromDays(1)),
                cache.FullName,
                "Novolis.IO.Unit/1.0");
            var key = new MapTileKey(2, 1, 2);

            var first = await source.GetTileAsync(key);
            var second = await source.GetTileAsync(key);

            await Assert.That(first).IsNotNull();
            await Assert.That(second).IsNotNull();
            await Assert.That(
                    first!.PngBytes.Length == 3
                    && first.PngBytes[0] == 1
                    && first.PngBytes[1] == 2
                    && first.PngBytes[2] == 3)
                .IsTrue();
            await Assert.That(handler.Requests).Count().IsEqualTo(1);
            await Assert.That(handler.Requests[0].ToString())
                .IsEqualTo("https://maps.example/2/1/2.png");
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task GeonorgeAddressSearch_reads_address_coordinates()
    {
        const string json =
            """
            {"adresser":[{"adressetekst":"Dronningens gate 1","representasjonspunkt":{"lat":58.14623,"lon":7.99517}}]}
            """;
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        using var client = new HttpClient(handler);

        var results = await new GeonorgeAddressSearch(client).SearchAsync("Dronningens gate 1");

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].DisplayName).IsEqualTo("Dronningens gate 1");
        await Assert.That(results[0].Coordinate.Latitude).IsEqualTo(58.14623d);
    }

    [Test]
    public async Task NominatimPlaceSearch_reads_string_coordinates()
    {
        const string json =
            """
            [{"display_name":"Kristiansand, Norway","lat":"58.14623","lon":"7.99517"}]
            """;
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        using var client = new HttpClient(handler);

        var results = await new NominatimPlaceSearch(client).SearchAsync("Kristiansand");

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].DisplayName).IsEqualTo("Kristiansand, Norway");
        await Assert.That(results[0].Coordinate.Longitude).IsEqualTo(7.99517d);
    }
}
