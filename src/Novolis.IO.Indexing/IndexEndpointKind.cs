namespace Novolis.IO.Indexing;

/// <summary>What an index endpoint refers to.</summary>
public enum IndexEndpointKind
{
    /// <summary>A document registered in the index.</summary>
    Document = 0,

    /// <summary>A conceptual entry (person, place, topic, … — caller-defined).</summary>
    Entry = 1,
}
