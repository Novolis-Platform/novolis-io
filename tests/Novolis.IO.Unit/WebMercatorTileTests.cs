using Novolis.IO.Maps;
using Novolis.Math.Geometry;

namespace Novolis.IO.Unit;

public sealed class WebMercatorTileTests
{
    [Test]
    public async Task Tile_key_wraps_x_and_validates_y()
    {
        var key = new MapTileKey(3, -1, 2);

        await Assert.That(key.X).IsEqualTo(7);
        await Assert.That(key.Y).IsEqualTo(2);
        await Assert.That(() => new MapTileKey(3, 0, 8))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Tiles_round_trip_a_kristiansand_coordinate()
    {
        var center = new GeoCoordinate(58.14623, 7.99517);
        var coordinate = new GeoCoordinate(58.15, 8.01);
        var pixel = WebMercatorTiles.GeoToPixel(
            center,
            zoom: 12,
            width: 800,
            height: 600,
            coordinate);
        var roundTrip = WebMercatorTiles.PixelToGeo(
            center,
            zoom: 12,
            width: 800,
            height: 600,
            pixel.X,
            pixel.Y);

        await Assert.That(pixel.X).IsGreaterThan(400d);
        await Assert.That(pixel.Y).IsLessThan(300d);
        await Assert.That(roundTrip.Latitude).IsEqualTo(coordinate.Latitude).Within(1e-9);
        await Assert.That(roundTrip.Longitude).IsEqualTo(coordinate.Longitude).Within(1e-9);
    }

    [Test]
    public async Task Tiles_keep_antimeridian_coordinates_nearby()
    {
        var pixel = WebMercatorTiles.GeoToPixel(
            new GeoCoordinate(0, 179.9),
            zoom: 8,
            width: 800,
            height: 600,
            new GeoCoordinate(0, -179.9));

        await Assert.That(pixel.X).IsGreaterThan(300d);
        await Assert.That(pixel.X).IsLessThan(500d);
    }

    [Test]
    public async Task Tiles_unwrap_connected_paths_at_the_dateline()
    {
        var pixels = WebMercatorTiles.GeoPathToPixels(
            new GeoCoordinate(0, 0),
            zoom: 2,
            width: 800,
            height: 600,
            [
                new GeoCoordinate(0, 179),
                new GeoCoordinate(0, -179),
            ]);

        await Assert.That(pixels).Count().IsEqualTo(2);
        await Assert.That(global::System.Math.Abs(pixels[1].X - pixels[0].X))
            .IsLessThan(100d);
    }

    [Test]
    public async Task Tiles_list_wrapped_tiles_and_clip_polar_rows()
    {
        var visible = WebMercatorTiles.VisibleTiles(
            new GeoCoordinate(0, 179),
            zoom: 3,
            width: 512,
            height: 512);

        await Assert.That(visible).IsNotEmpty();
        await Assert.That(visible.All(tile => tile.Zoom == 3 && tile.Y is >= 0 and < 8))
            .IsTrue();
    }

    [Test]
    public async Task Tiles_return_stable_exact_keys_at_world_quarter()
    {
        var visible = WebMercatorTiles.VisibleTiles(
            new GeoCoordinate(0, 0),
            zoom: 1,
            width: 256,
            height: 256);

        await Assert.That(visible).IsEquivalentTo(
        [
            new MapTileKey(1, 0, 0),
            new MapTileKey(1, 0, 1),
            new MapTileKey(1, 1, 0),
            new MapTileKey(1, 1, 1),
        ]);
        await Assert.That(visible.ToArray()).IsEquivalentTo(
            WebMercatorTiles.VisibleTiles(
                new GeoCoordinate(0, 0),
                zoom: 1,
                width: 256,
                height: 256));
    }

    [Test]
    public async Task Tiles_exclude_tile_on_exact_right_and_bottom_boundary()
    {
        var center = WebMercatorProjection.Unproject(
            new GeoProjectedPoint(0.375, 0.375));
        var visible = WebMercatorTiles.VisibleTiles(
            center,
            zoom: 2,
            width: 256,
            height: 256);

        await Assert.That(visible).IsEquivalentTo(
            [new MapTileKey(2, 1, 1)]);
    }

    [Test]
    public async Task Tiles_use_the_nearest_integer_tile_zoom()
    {
        var visible = WebMercatorTiles.VisibleTiles(
            new GeoCoordinate(0, 0),
            zoom: 2.5,
            width: 256,
            height: 256);

        await Assert.That(visible.All(tile => tile.Zoom == 3)).IsTrue();
        await Assert.That(visible).Count().IsEqualTo(4);
    }

    [Test]
    public async Task Tiles_deduplicate_a_viewport_wider_than_the_world()
    {
        var visible = WebMercatorTiles.VisibleTiles(
            new GeoCoordinate(0, 0),
            zoom: 2,
            width: 2_048,
            height: 512);

        await Assert.That(visible).Count().IsEqualTo(8);
        await Assert.That(visible.Select(tile => tile.X).Distinct().Count())
            .IsEqualTo(4);
        await Assert.That(visible.Select(tile => tile.Y).Distinct().Count())
            .IsEqualTo(2);
    }
}
