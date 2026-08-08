<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-io">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Indexing

In-memory, **format-agnostic** content index: documents, entries, aliases, open facets, and directed links.

No Markdown, YAML, or manuscript protocol — hosts supply ids, aliases, facets, and links from whatever source they use.

## Install

```powershell
dotnet add package Novolis.IO.Indexing
```

## Quick start

```csharp
using Novolis.IO.Indexing;

var index = new ContentIndexBuilder()
    .AddDocument("doc:prologue", location: "workspace/prologue")
    .AddEntry("calypso", title: "Calypso", aliases: ["the tramp", "Calypso"])
    .AddFacet("calypso", "kind", "ship")
    .AddLink(
        new IndexEndpoint(IndexEndpointKind.Document, "doc:prologue"),
        new IndexEndpoint(IndexEndpointKind.Entry, "calypso"),
        relation: "mentions")
    .Build();

index.TryResolveEntry("the tramp", out var entry);
var ships = index.FindByFacet("kind", "ship");
```

## API

| Type | Role |
|------|------|
| `ContentIndexBuilder` | Mutable accumulate → `Build()` snapshot |
| `ContentIndex` | Immutable query: resolve alias, facet filter, links |
| `IndexEntry` / `IndexDocument` / `IndexLink` | DTOs |
| `IndexSpan` | Opaque provenance offsets (caller-defined units) |

Domain layers (e.g. `Novolis.Manuscript.References`) compose this package; they do not live here.

## Library vs CLI

Analysis stays in libraries. Tools only map argv → these APIs.
