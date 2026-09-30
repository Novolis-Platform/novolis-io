namespace Novolis.IO.Git;

/// <summary>Lane metadata.</summary>
public sealed class CommitLane
{
    /// <summary>Lane index.</summary>
    public required int Index { get; init; }

    /// <summary>Optional branch name hint.</summary>
    public string? TipName { get; init; }
}
