using Novolis.IO.Indexing;

namespace Novolis.IO.Unit;

public sealed class ContentIndexTests
{
    [Test]
    public async Task Resolve_prefers_id_and_aliases()
    {
        var index = new ContentIndexBuilder()
            .AddEntry("calypso", title: "Calypso", aliases: ["the tramp", "Calypso"])
            .Build();

        await Assert.That(index.TryResolveEntry("calypso", out var byId)).IsTrue();
        await Assert.That(byId.Title).IsEqualTo("Calypso");
        await Assert.That(index.TryResolveEntry("the tramp", out var byAlias)).IsTrue();
        await Assert.That(byAlias.Id).IsEqualTo("calypso");
        await Assert.That(index.TryResolveEntry("missing", out _)).IsFalse();
    }

    [Test]
    public async Task Facets_and_links_query()
    {
        var index = new ContentIndexBuilder()
            .AddDocument("ch1", "path/ch1")
            .AddEntry("calypso")
            .AddFacet("calypso", "kind", "ship")
            .AddFacet("calypso", "kind", "vessel")
            .AddEntry("mira")
            .AddFacet("mira", "kind", "person")
            .AddLink(
                new IndexEndpoint(IndexEndpointKind.Document, "ch1"),
                new IndexEndpoint(IndexEndpointKind.Entry, "calypso"),
                relation: "mentions",
                provenanceDocumentId: "ch1",
                span: new IndexSpan(10, 7))
            .Build();

        var ships = index.FindByFacet("kind", "ship").Select(e => e.Id).ToArray();
        await Assert.That(ships).IsEquivalentTo(["calypso"]);

        var fromDoc = index.GetLinksFrom(new IndexEndpoint(IndexEndpointKind.Document, "ch1")).ToArray();
        await Assert.That(fromDoc.Length).IsEqualTo(1);
        await Assert.That(fromDoc[0].To.Id).IsEqualTo("calypso");
        await Assert.That(fromDoc[0].Span).IsEqualTo(new IndexSpan(10, 7));

        var mentions = index.GetLinksByRelation("mentions").ToArray();
        await Assert.That(mentions.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Merge_accumulates_aliases_and_facets()
    {
        var index = new ContentIndexBuilder()
            .AddEntry("x", title: "One", aliases: ["a"])
            .AddAlias("x", "b")
            .AddFacet("x", "tag", "alpha")
            .AddEntry("x", title: "Two", aliases: ["a", "c"], facets: new Dictionary<string, IReadOnlyList<string>>
            {
                ["tag"] = ["beta"],
            })
            .Build();

        await Assert.That(index.TryGetEntry("x", out var entry)).IsTrue();
        await Assert.That(entry.Title).IsEqualTo("Two");
        await Assert.That(entry.Aliases).IsEquivalentTo(["a", "b", "c"]);
        await Assert.That(entry.Facets!["tag"]).IsEquivalentTo(["alpha", "beta"]);
    }
}
