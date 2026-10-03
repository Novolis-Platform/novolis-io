using Novolis.IO.Maps;
using Novolis.Math.Geometry;

namespace Novolis.IO.Unit;

public sealed class MapInteractionContractTests
{
    [Test]
    public async Task Overlay_keys_are_type_qualified_and_reject_blank_ids()
    {
        var marker = new MapOverlayKey(MapOverlayKind.Marker, "shared-id");
        var polygon = new MapOverlayKey(MapOverlayKind.Polygon, "shared-id");

        await Assert.That(marker).IsNotEqualTo(polygon);
        await Assert.That(marker.ToString()).IsEqualTo("Marker:shared-id");
        await Assert.That(
                () => new MapOverlayKey(MapOverlayKind.Track, " "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Rectangle_drawing_exposes_closed_geometry_and_statistics()
    {
        var drawing = new GeoDrawing(
            GeoDrawingKind.Rectangle,
            [
                new GeoCoordinate(58.10, 7.90),
                new GeoCoordinate(58.20, 8.10),
            ]);

        await Assert.That(drawing.Rectangle).IsNotNull();
        await Assert.That(drawing.ClosedPoints).Count().IsEqualTo(5);
        await Assert.That(drawing.Statistics.VertexCount).IsEqualTo(4);
        await Assert.That(drawing.Statistics.LengthMeters).IsGreaterThan(1_000);
        await Assert.That(drawing.Statistics.AreaSquareMeters).IsGreaterThan(1_000_000);
    }

    [Test]
    public async Task Rectangle_requires_opposite_corners_and_formats_preview_measurement()
    {
        await Assert.That(
                () => new GeoDrawing(
                    GeoDrawingKind.Rectangle,
                    [new GeoCoordinate(58.10, 7.90)]))
            .Throws<ArgumentException>();

        var preview = GeoMeasurementText.ForDrawing(
            GeoDrawingKind.Rectangle,
            [
                new GeoCoordinate(58.10, 7.90),
                new GeoCoordinate(58.20, 8.10),
            ]);

        await Assert.That(preview).Contains("Rectangle");
        await Assert.That(preview).Contains("area");
    }

    [Test]
    public async Task Keyboard_contract_includes_overlay_erasure()
    {
        await Assert.That(Enum.GetValues<MapKeyboardCommand>())
            .Contains(MapKeyboardCommand.EraseSelectedOverlay);
    }

    [Test]
    public async Task Drawing_calculates_circle_length_and_area()
    {
        var center = new GeoCoordinate(0, 0);
        var edge = new GeoCoordinate(0, 0.01);
        var drawing = new GeoDrawing(
            GeoDrawingKind.Circle,
            [center, edge]);

        await Assert.That(drawing.RadiusMeters!.Value).IsEqualTo(1_111.9d).Within(2d);
        await Assert.That(drawing.LengthMeters).IsGreaterThan(6_000d);
        await Assert.That(drawing.AreaSquareMeters).IsGreaterThan(3_000_000d);
    }

    [Test]
    public async Task Measurement_text_formats_distance_area_and_preview()
    {
        var points = new[]
        {
            new GeoCoordinate(0, 0),
            new GeoCoordinate(0, 0.01),
            new GeoCoordinate(0.01, 0.01),
        };

        await Assert.That(GeoMeasurementText.FormatDistance(1_234.5d))
            .IsEqualTo("1.23 km");
        await Assert.That(GeoMeasurementText.FormatArea(1_234_567d))
            .IsEqualTo("1.23 km²");
        await Assert.That(GeoMeasurementText.ForDrawing(
                GeoDrawingKind.Polygon,
                points))
            .Contains("perimeter");
    }

    [Test]
    public async Task Coordinate_text_formats_invariant_text_and_json()
    {
        var coordinate = new GeoCoordinate(58.14623, 7.99517);

        await Assert.That(GeoCoordinateText.Format(coordinate))
            .IsEqualTo("58.146230, 7.995170");
        await Assert.That(GeoCoordinateText.ToJson(coordinate, "office", "Office"))
            .Contains("\"latitude\": 58.14623");
    }
}
