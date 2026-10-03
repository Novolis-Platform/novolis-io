using System.Globalization;
using System.Text.Json;
using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>Stable human and machine-readable geographic coordinate formats.</summary>
public static class GeoCoordinateText
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>Formats a coordinate with invariant decimal separators.</summary>
    public static string Format(
        GeoCoordinate coordinate,
        int decimalPlaces = 6)
    {
        if (decimalPlaces is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces));

        var format = $"F{decimalPlaces}";
        return coordinate.Latitude.ToString(format, CultureInfo.InvariantCulture)
            + ", "
            + coordinate.Longitude.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>Serializes a selected coordinate and optional marker identity as JSON.</summary>
    public static string ToJson(
        GeoCoordinate coordinate,
        string? id = null,
        string? label = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        JsonSerializer.Serialize(
            new
            {
                id,
                label,
                latitude = coordinate.Latitude,
                longitude = coordinate.Longitude,
                metadata,
            },
            JsonOptions);
}
