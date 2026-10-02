using Novolis.Math.Geometry;

namespace Novolis.IO.Maps;

/// <summary>A place returned by a map search service.</summary>
public sealed record MapPlace(string DisplayName, GeoCoordinate Coordinate);
