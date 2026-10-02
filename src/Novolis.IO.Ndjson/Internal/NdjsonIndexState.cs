namespace Novolis.IO.Ndjson;

internal sealed class NdjsonIndexState
{
    public NdjsonIndexState(NdjsonFileIdentity identity)
    {
        Identity = identity;
        Entries = [new NdjsonIndexEntry(0, 0)];
    }

    public NdjsonFileIdentity Identity { get; set; }

    public long IndexedLength { get; set; }

    public long RecordCount { get; set; }

    public long LastCompleteRecordOffset { get; set; }

    internal long LastScanBytes { get; set; }

    public List<NdjsonIndexEntry> Entries { get; } = [];

    public NdjsonIndexSnapshot Snapshot() =>
        new(
            IndexedLength,
            RecordCount,
            Identity.Length,
            Entries.ToArray());
}
