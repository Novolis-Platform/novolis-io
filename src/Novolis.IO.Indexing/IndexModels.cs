namespace Novolis.IO.Indexing;

/// <summary>Opaque span inside a document (caller-defined units: bytes, chars, or tokens).</summary>
/// <param name="Start">Start offset.</param>
/// <param name="Length">Length from <paramref name="Start"/>.</param>
public readonly record struct IndexSpan(int Start, int Length);
