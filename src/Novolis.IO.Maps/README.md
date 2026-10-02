# Novolis.IO.Maps

Provider-neutral HTTP and disk-cache clients for Web Mercator XYZ raster
tiles and place search.

The package has no Avalonia or MAUI dependency. A view receives
`MapRasterTile` PNG bytes and decodes them in its own graphics stack.

## Tile presets

`MapPresets` includes:

- Kartverket `topo`, `topograatone`, `toporaster`, and `sjokartraster`
- OpenStreetMap standard
- OpenTopoMap
- CyclOSM

Every template carries its URL, attribution, optional subdomains, and minimum
cache age. The cache is partitioned by provider name and tile coordinates.
The source returns a stale cached tile when a refresh fails.

OpenStreetMap standard tiles must follow the
[OSMF tile usage policy](https://operations.osmfoundation.org/policies/tiles/).
Callers must provide a stable identifying User-Agent and show the attribution
from the selected template.

## Place search

`GeonorgeAddressSearch` searches Norwegian addresses. `NominatimPlaceSearch`
searches OpenStreetMap places and serializes requests by at least one second,
as required by the public Nominatim service policy. Search is deliberately
separate from tile selection.

## Install

```bash
dotnet add package Novolis.IO.Maps
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`).

## Quick start

```csharp
using Novolis.IO.Maps;
using Novolis.Math.Geometry;

using var http = new HttpClient();
using var source = new XyzMapSource(
    http,
    MapPresets.OpenStreetMapStandard,
    cacheDirectory: Path.Combine(Path.GetTempPath(), "map-cache"),
    userAgent: "example-app/1.0 (contact: maps@example.com)");

MapRasterTile? tile = await source.GetTileAsync(new MapTileKey(zoom: 3, x: 4, y: 5));
```

Show `source.Template.Attribution` next to the map. OpenStreetMap standard tiles follow the OSMF tile usage policy.
