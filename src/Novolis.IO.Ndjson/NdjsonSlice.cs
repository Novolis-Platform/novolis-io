namespace Novolis.IO.Ndjson;

/// <summary>A bounded view over a record range.</summary>
public sealed record NdjsonSlice(
    long Skip,
    int Take,
    IReadOnlyList<NdjsonRecord> Records,
    bool HasPrevious,
    bool HasMore);
