using System.Text.Json;

namespace Novolis.IO.Ndjson;

/// <summary>A physical NDJSON line, whether JSON parsing succeeded or failed.</summary>
public sealed record NdjsonRecord(
    long Number,
    long ByteOffset,
    JsonElement? Json,
    string? Raw,
    JsonException? Error)
{
    /// <summary>Whether this physical line parsed as JSON.</summary>
    public bool IsValid => Error is null;
}
