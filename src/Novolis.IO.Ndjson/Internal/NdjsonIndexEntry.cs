namespace Novolis.IO.Ndjson;

internal readonly record struct NdjsonIndexEntry(
    long RecordNumber,
    long ByteOffset);
