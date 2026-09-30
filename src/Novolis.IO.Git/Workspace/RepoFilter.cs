namespace Novolis.IO.Git;

/// <summary>Filter for workspace repo selection.</summary>
public sealed class RepoFilter
{
    /// <summary>Include only these names (short or novolis-*); empty = all.</summary>
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>Exclude these names.</summary>
    public IReadOnlyList<string>? Exclude { get; init; }

    /// <summary>Only dirty repos.</summary>
    public bool? Dirty { get; init; }

    /// <summary>Only repos behind upstream.</summary>
    public bool? Behind { get; init; }

    /// <summary>Only repos ahead of upstream.</summary>
    public bool? Ahead { get; init; }

    /// <summary>Only repos on this branch name.</summary>
    public string? OnBranch { get; init; }
}
