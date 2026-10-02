using System.Net;
using System.Text;
using Novolis.IO.Maps;
using Novolis.Math.Geometry;

namespace Novolis.IO.Unit;

public sealed class MapProviderTests
{
    static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

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
        await Assert.That(MapPresets.KartverketTopo.AxisOrder)
            .IsEqualTo(MapTileAxisOrder.YThenX);
        await Assert.That(MapPresets.OpenStreetMapStandard.AxisOrder)
            .IsEqualTo(MapTileAxisOrder.XThenY);
        await Assert.That(MapPresets.All.All(
                static preset => !string.IsNullOrWhiteSpace(preset.Attribution)))
            .IsTrue();
    }

    [Test]
    public async Task XyzMapTemplate_validates_placeholders_and_preserves_explicit_axis_order()
    {
        var xThenY = new XyzMapTemplate(
            "xy",
            "https://maps.example/{z}/{x}/{y}.png",
            "Test",
            MapTileAxisOrder.XThenY);
        var yThenX = new XyzMapTemplate(
            "yx",
            "https://maps.example/{z}/{y}/{x}.png",
            "Test",
            MapTileAxisOrder.YThenX);
        var key = new MapTileKey(3, 4, 5);

        await Assert.That(xThenY.BuildUri(key).ToString())
            .IsEqualTo("https://maps.example/3/4/5.png");
        await Assert.That(yThenX.BuildUri(key).ToString())
            .IsEqualTo("https://maps.example/3/5/4.png");
        await Assert.That(() => new XyzMapTemplate(
                "missing",
                "https://maps.example/{z}/{x}.png",
                "Test"))
            .Throws<ArgumentException>();
        await Assert.That(() => new XyzMapTemplate(
                "subdomain",
                "https://{s}.maps.example/{z}/{x}/{y}.png",
                "Test"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task XyzMapSource_uses_a_fresh_cache_without_second_request()
    {
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-test-");
        try
        {
            using var source = new XyzMapSource(
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
                    first!.PngBytes.Length == ValidPng.Length
                    && first.PngBytes[0] == ValidPng[0]
                    && first.PngBytes[7] == ValidPng[7])
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
    public async Task XyzMapSource_rejects_invalid_content_and_oversized_responses()
    {
        var invalidHandler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not a tile", Encoding.UTF8, "application/json"),
            });
        using var invalidClient = new HttpClient(invalidHandler);
        var invalidCache = Directory.CreateTempSubdirectory("novolis-map-invalid-");
        try
        {
            using var invalidSource = new XyzMapSource(
                invalidClient,
                new XyzMapTemplate(
                    "invalid-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                invalidCache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions { MinimumRequestInterval = TimeSpan.Zero });

            await Assert.That(await invalidSource.GetTileAsync(new MapTileKey(1, 0, 0)))
                .IsNull();
        }
        finally
        {
            invalidCache.Delete(recursive: true);
        }

        var malformedHandler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(
                    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            });
        using var malformedClient = new HttpClient(malformedHandler);
        var malformedCache = Directory.CreateTempSubdirectory("novolis-map-malformed-");
        try
        {
            using var malformedSource = new XyzMapSource(
                malformedClient,
                new XyzMapTemplate(
                    "malformed-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                malformedCache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions { MinimumRequestInterval = TimeSpan.Zero });

            await Assert.That(await malformedSource.GetTileAsync(new MapTileKey(1, 0, 0)))
                .IsNull();
        }
        finally
        {
            malformedCache.Delete(recursive: true);
        }

        var oversizedHandler = new MapTestHttpHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            };
            response.Content.Headers.ContentLength = ValidPng.Length;
            return response;
        });
        using var oversizedClient = new HttpClient(oversizedHandler);
        var oversizedCache = Directory.CreateTempSubdirectory("novolis-map-oversized-");
        try
        {
            using var oversizedSource = new XyzMapSource(
                oversizedClient,
                new XyzMapTemplate(
                    "oversized-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                oversizedCache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions
                {
                    MaximumTileBytes = ValidPng.Length - 1,
                    MinimumRequestInterval = TimeSpan.Zero,
                });

            await Assert.That(await oversizedSource.GetTileAsync(new MapTileKey(1, 0, 0)))
                .IsNull();
        }
        finally
        {
            oversizedCache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task XyzMapSource_sets_user_agent_and_rejects_invalid_options()
    {
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-options-");
        try
        {
            using var source = new XyzMapSource(
                client,
                new XyzMapTemplate(
                    "options-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                cache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions { MinimumRequestInterval = TimeSpan.Zero });

            await Assert.That(client.DefaultRequestHeaders.UserAgent.ToString())
                .Contains("Novolis.IO.Unit/1.0");
        }
        finally
        {
            cache.Delete(recursive: true);
        }

        var invalidCache = Directory.CreateTempSubdirectory("novolis-map-options-invalid-");
        try
        {
            await Assert.That(() => new XyzMapSource(
                    client,
                    MapPresets.OpenStreetMapStandard,
                    invalidCache.FullName,
                    "Novolis.IO.Unit/1.0",
                    new XyzMapSourceOptions { MaximumConcurrentRequests = 0 }))
                .Throws<ArgumentOutOfRangeException>();
        }
        finally
        {
            invalidCache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task XyzMapSource_uses_a_stale_tile_when_refresh_fails()
    {
        var calls = 0;
        var handler = new MapTestHttpHandler(_ =>
        {
            if (Interlocked.Increment(ref calls) > 1)
                throw new HttpRequestException("offline");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            };
        });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-stale-");
        try
        {
            using var source = new XyzMapSource(
                client,
                new XyzMapTemplate(
                    "stale-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                cache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions
                {
                    MinimumRequestInterval = TimeSpan.Zero,
                    MaximumStaleAge = TimeSpan.FromDays(1),
                });

            var key = new MapTileKey(2, 1, 2);
            var first = await source.GetTileAsync(key);
            var second = await source.GetTileAsync(key);

            await Assert.That(first).IsNotNull();
            await Assert.That(second).IsNotNull();
            await Assert.That(second!.IsStale).IsTrue();
            await Assert.That(handler.Requests).Count().IsEqualTo(2);
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task XyzMapSource_rejects_future_cache_entries()
    {
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-future-");
        try
        {
            using var source = new XyzMapSource(
                client,
                new XyzMapTemplate(
                    "future-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test",
                    minimumCacheAge: TimeSpan.FromDays(1)),
                cache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions
                {
                    MinimumRequestInterval = TimeSpan.Zero,
                });

            var key = new MapTileKey(2, 1, 2);
            await Assert.That(await source.GetTileAsync(key)).IsNotNull();
            var path = Directory.EnumerateFiles(cache.FullName, "*.png", SearchOption.AllDirectories)
                .Single();
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

            await Assert.That(await source.GetTileAsync(key)).IsNotNull();
            await Assert.That(handler.Requests).Count().IsEqualTo(2);
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task XyzMapSource_evicts_old_tiles_when_the_cache_limit_is_reached()
    {
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-eviction-");
        try
        {
            using var source = new XyzMapSource(
                client,
                new XyzMapTemplate(
                    "eviction-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                cache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions
                {
                    MinimumRequestInterval = TimeSpan.Zero,
                    MaximumCachedTiles = 1,
                    MinimumEvictionInterval = TimeSpan.Zero,
                });

            await Assert.That(
                    await source.GetTileAsync(new MapTileKey(2, 1, 2)))
                .IsNotNull();
            await Assert.That(
                    await source.GetTileAsync(new MapTileKey(2, 2, 2)))
                .IsNotNull();

            await Assert.That(
                    Directory.EnumerateFiles(
                        cache.FullName,
                        "*.png",
                        SearchOption.AllDirectories)
                    .Count())
                .IsEqualTo(1);
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task XyzMapSource_evicts_tiles_when_the_byte_limit_is_reached()
    {
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-byte-eviction-");
        try
        {
            using var source = new XyzMapSource(
                client,
                new XyzMapTemplate(
                    "byte-eviction-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                cache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions
                {
                    MinimumRequestInterval = TimeSpan.Zero,
                    MaximumCachedTiles = 10,
                    MaximumCacheBytes = ValidPng.Length + 1,
                    MinimumEvictionInterval = TimeSpan.Zero,
                });

            await Assert.That(
                    await source.GetTileAsync(new MapTileKey(2, 1, 2)))
                .IsNotNull();
            await Assert.That(
                    await source.GetTileAsync(new MapTileKey(2, 2, 2)))
                .IsNotNull();

            var files = Directory.EnumerateFiles(
                    cache.FullName,
                    "*.png",
                    SearchOption.AllDirectories)
                .ToArray();
            await Assert.That(files).Count().IsEqualTo(1);
            await Assert.That(new FileInfo(files[0]).Length)
                .IsLessThanOrEqualTo(ValidPng.Length);
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task XyzMapSource_single_flights_same_tile_requests()
    {
        var handler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPng),
            });
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-flight-");
        try
        {
            using var source = new XyzMapSource(
                client,
                new XyzMapTemplate(
                    "flight-map",
                    "https://maps.example/{z}/{x}/{y}.png",
                    "Test"),
                cache.FullName,
                "Novolis.IO.Unit/1.0",
                new XyzMapSourceOptions
                {
                    MinimumRequestInterval = TimeSpan.Zero,
                });

            var key = new MapTileKey(2, 1, 2);
            var results = await Task.WhenAll(
                Enumerable.Range(0, 8)
                    .Select(_ => source.GetTileAsync(key).AsTask()));

            await Assert.That(results.All(result => result is not null)).IsTrue();
            await Assert.That(handler.Requests).Count().IsEqualTo(1);
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    [Test]
    public async Task XyzMapSource_cancellation_cancels_only_the_waiting_caller()
    {
        var handler = new BlockingMapTestHttpHandler();
        using var client = new HttpClient(handler);
        var cache = Directory.CreateTempSubdirectory("novolis-map-cancel-");
        var source = new XyzMapSource(
            client,
            new XyzMapTemplate(
                "cancel-map",
                "https://maps.example/{z}/{x}/{y}.png",
                "Test"),
            cache.FullName,
            "Novolis.IO.Unit/1.0",
            new XyzMapSourceOptions { MinimumRequestInterval = TimeSpan.Zero });
        using var callerCancellation = new CancellationTokenSource();
        try
        {
            var request = source.GetTileAsync(
                    new MapTileKey(2, 1, 2),
                    callerCancellation.Token)
                .AsTask();
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

            callerCancellation.Cancel();
            await Assert.That(async () => await request)
                .Throws<OperationCanceledException>();

            handler.Response.TrySetResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(ValidPng),
                });
            await handler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.That(await source.GetTileAsync(new MapTileKey(2, 1, 2)))
                .IsNotNull();
        }
        finally
        {
            source.Dispose();
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
        await Assert.That(handler.Requests[0].Query)
            .Contains("sok=Dronningens%20gate%201");
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
        await Assert.That(handler.Requests[0].Query)
            .Contains("q=Kristiansand");
    }

    [Test]
    public async Task Place_searches_ignore_malformed_records_and_empty_documents()
    {
        const string geonorgeJson =
            """
            {"adresser":[
              {"adressetekst":"Missing point"},
              {"adressetekst":"Outside","representasjonspunkt":{"lat":95,"lon":7}},
              {"adressetekst":"Valid","representasjonspunkt":{"lat":58,"lon":7}}
            ]}
            """;
        var geonorgeHandler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    geonorgeJson,
                    Encoding.UTF8,
                    "application/json"),
            });
        using var geonorgeClient = new HttpClient(geonorgeHandler);

        var geonorgeResults = await new GeonorgeAddressSearch(geonorgeClient)
            .SearchAsync("valid");

        await Assert.That(geonorgeResults).Count().IsEqualTo(1);
        await Assert.That(geonorgeResults[0].DisplayName).IsEqualTo("Valid");

        var nominatimHandler = new MapTestHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        using var nominatimClient = new HttpClient(nominatimHandler);

        var nominatimResults = await new NominatimPlaceSearch(nominatimClient)
            .SearchAsync("empty");

        await Assert.That(nominatimResults).IsEmpty();
    }
}
