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
