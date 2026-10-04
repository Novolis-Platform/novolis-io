namespace Novolis.IO.Git;

/// <summary>How to choose git checkouts under a checkout root.</summary>
public enum GitDiscover
{
    /// <summary>Every immediate child directory that has a <c>.git</c> directory or file.</summary>
    GitChildren = 0,

    /// <summary>Immediate <c>novolis-*</c> children that have a <c>.git</c> directory or file.</summary>
    NovolisPrefix = 1,
}
