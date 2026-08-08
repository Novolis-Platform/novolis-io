namespace Novolis.IO.Indexing;

/// <summary>Mutable builder for an in-memory <see cref="ContentIndex"/>.</summary>
public sealed class ContentIndexBuilder
{
    readonly Dictionary<string, IndexDocument> _documents = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, MutableEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    readonly List<IndexLink> _links = [];

    /// <summary>Registers or replaces a document.</summary>
    public ContentIndexBuilder AddDocument(string id, string? location = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _documents[id] = new IndexDocument(id, location);
        return this;
    }

    /// <summary>Registers or replaces a document.</summary>
    public ContentIndexBuilder AddDocument(IndexDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Id);
        _documents[document.Id] = document;
        return this;
    }

    /// <summary>Registers or merges an entry (aliases and facets accumulate).</summary>
    public ContentIndexBuilder AddEntry(
        string id,
        string? title = null,
        IEnumerable<string>? aliases = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? facets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_entries.TryGetValue(id, out var entry))
        {
            entry = new MutableEntry(id);
            _entries[id] = entry;
        }

        if (!string.IsNullOrWhiteSpace(title))
            entry.Title = title.Trim();

        if (aliases is not null)
        {
            foreach (var alias in aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                    entry.Aliases.Add(alias.Trim());
            }
        }

        if (facets is not null)
        {
            foreach (var (key, values) in facets)
            {
                if (string.IsNullOrWhiteSpace(key) || values is null)
                    continue;
                if (!entry.Facets.TryGetValue(key, out var bag))
                {
                    bag = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    entry.Facets[key] = bag;
                }

                foreach (var value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        bag.Add(value.Trim());
                }
            }
        }

        return this;
    }

    /// <summary>Adds one alias for an existing or new entry.</summary>
    public ContentIndexBuilder AddAlias(string entryId, string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        return AddEntry(entryId, aliases: [alias]);
    }

    /// <summary>Adds one facet value for an existing or new entry.</summary>
    public ContentIndexBuilder AddFacet(string entryId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return AddEntry(entryId, facets: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = [value],
        });
    }

    /// <summary>Adds a directed link.</summary>
    public ContentIndexBuilder AddLink(IndexLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentException.ThrowIfNullOrWhiteSpace(link.From.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(link.To.Id);
        _links.Add(link);
        return this;
    }

    /// <summary>Adds a directed link between endpoints.</summary>
    public ContentIndexBuilder AddLink(
        IndexEndpoint from,
        IndexEndpoint to,
        string? relation = null,
        string? provenanceDocumentId = null,
        IndexSpan? span = null) =>
        AddLink(new IndexLink(from, to, relation, provenanceDocumentId, span));

    /// <summary>Builds an immutable snapshot.</summary>
    public ContentIndex Build()
    {
        var documents = _documents.ToDictionary(
            static kv => kv.Key,
            static kv => kv.Value,
            StringComparer.OrdinalIgnoreCase);

        var entries = new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);
        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mutable in _entries.Values)
        {
            var aliases = mutable.Aliases
                .Where(static a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static a => a, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var facets = mutable.Facets.ToDictionary(
                static kv => kv.Key,
                static kv => (IReadOnlyList<string>)kv.Value
                    .OrderBy(static v => v, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

            var entry = new IndexEntry(mutable.Id, mutable.Title, aliases, facets);
            entries[mutable.Id] = entry;

            aliasMap[mutable.Id] = mutable.Id;
            foreach (var alias in aliases)
                aliasMap[alias] = mutable.Id;
        }

        var links = _links.ToArray();
        return new ContentIndex(documents, entries, aliasMap, links);
    }

    sealed class MutableEntry(string id)
    {
        public string Id { get; } = id;
        public string? Title { get; set; }
        public HashSet<string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> Facets { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
