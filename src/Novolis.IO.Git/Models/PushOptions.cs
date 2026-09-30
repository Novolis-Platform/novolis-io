namespace Novolis.IO.Git;

/// <summary>Options for push.</summary>
public sealed class PushOptions
{
    /// <summary>Remote name.</summary>
    public string Remote { get; init; } = "origin";

    /// <summary>Set upstream (-u).</summary>
    public bool SetUpstream { get; init; }

    /// <summary>Never force by default; force requires explicit true.</summary>
    public bool Force { get; init; }
}
