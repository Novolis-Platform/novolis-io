namespace Novolis.IO.Git;

/// <summary>Kind of git tip ref.</summary>
public enum GitRefKind
{
    /// <summary>Local branch.</summary>
    Branch,

    /// <summary>Remote-tracking branch.</summary>
    Remote,

    /// <summary>Tag.</summary>
    Tag,
}
