namespace Novolis.IO.Ndjson;

internal sealed record NdjsonIndexSnapshot(
    long IndexedLength,
    long RecordCount,
    long SourceLength,
    IReadOnlyList<NdjsonIndexEntry> Entries);
