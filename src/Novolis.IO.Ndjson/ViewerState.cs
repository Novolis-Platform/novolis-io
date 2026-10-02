namespace Novolis.IO.Ndjson;

/// <summary>UI-neutral state for navigating one bounded NDJSON slice.</summary>
public sealed record ViewerState(
    long Skip,
    int Take,
    NdjsonSlice? Slice,
    bool IsRefreshing,
    Exception? Error);
