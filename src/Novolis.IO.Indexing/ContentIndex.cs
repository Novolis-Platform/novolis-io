namespace Novolis.IO.Indexing;

/// <summary>Immutable in-memory content index (documents, entries, aliases, links).</summary>
public sealed class ContentIndex
{
    readonly Dictionary<string, IndexDocument> _documents;
    readonly Dictionary<string, IndexEntry> _entries;
    readonly Dictionary<string, string> _aliasToEntryId;
    readonly IndexLink[] _links;

    internal ContentIndex(
        Dictionary<string, IndexDocument> documents,
        Dictionary<string, IndexEntry> entries,
        Dictionary<string, string> aliasToEntryId,
        IndexLink[] links)
    {
        _documents = documents;
        _entries = entries;
        _aliasToEntryId = aliasToEntryId;
        _links = links;
    }

    /// <summary>Empty index.</summary>
    public static ContentIndex Empty { get; } = new(
        new Dictionary<string, IndexDocument>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        []);

    /// <summary>All documents.</summary>
    public IReadOnlyCollection<IndexDocument> Documents => _documents.Values;

    /// <summary>All entries.</summary>
    public IReadOnlyCollection<IndexEntry> Entries => _entries.Values;

    /// <summary>All links.</summary>
    public IReadOnlyList<IndexLink> Links => _links;

    /// <summary>Tries to get a document by id.</summary>
    public bool TryGetDocument(string id, out IndexDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _documents.TryGetValue(id, out document!);
    }

    /// <summary>Tries to get an entry by id.</summary>
    public bool TryGetEntry(string id, out IndexEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _entries.TryGetValue(id, out entry!);
    }

    /// <summary>
    /// Resolves an id or alias to an entry. Exact id wins; otherwise alias map.
    /// </summary>
    public bool TryResolveEntry(string idOrAlias, out IndexEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrAlias);
        if (_entries.TryGetValue(idOrAlias, out entry!))
            return true;
        if (_aliasToEntryId.TryGetValue(idOrAlias, out var id) && _entries.TryGetValue(id, out entry!))
            return true;
        entry = null!;
        return false;
    }

    /// <summary>Entries that carry a facet key/value pair.</summary>
    public IEnumerable<IndexEntry> FindByFacet(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        foreach (var entry in _entries.Values)
        {
            if (entry.Facets is null)
                continue;
            if (!entry.Facets.TryGetValue(key, out var values))
                continue;
            if (values.Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)))
                yield return entry;
        }
    }

    /// <summary>Links whose <see cref="IndexLink.From"/> matches.</summary>
    public IEnumerable<IndexLink> GetLinksFrom(IndexEndpoint from) =>
        _links.Where(l => l.From.Kind == from.Kind
                          && string.Equals(l.From.Id, from.Id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Links whose <see cref="IndexLink.To"/> matches.</summary>
    public IEnumerable<IndexLink> GetLinksTo(IndexEndpoint to) =>
        _links.Where(l => l.To.Kind == to.Kind
                          && string.Equals(l.To.Id, to.Id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Links with the given relation (case-insensitive). Null relation matches links with no relation.</summary>
    public IEnumerable<IndexLink> GetLinksByRelation(string? relation)
    {
        if (relation is null)
            return _links.Where(static l => l.Relation is null);
        return _links.Where(l => string.Equals(l.Relation, relation, StringComparison.OrdinalIgnoreCase));
    }
}
