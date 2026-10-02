namespace Novolis.IO.Ndjson;

/// <summary>Bounds and cadence for a seekable NDJSON document.</summary>
public sealed record NdjsonOpenOptions(
    int IndexInterval = 10_000,
    int MaxTake = 2_000)
{
    internal const int MaxRecordBytes = 32 * 1024 * 1024;

    internal void Validate()
    {
        if (IndexInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(IndexInterval), "The index interval must be positive.");

        if (MaxTake <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxTake), "The maximum slice size must be positive.");
    }
}
