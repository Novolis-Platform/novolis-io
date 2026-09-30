namespace Novolis.IO.Git;

/// <summary>Working tree path group.</summary>
public enum WorkingTreeGroup
{
    /// <summary>Staged for commit.</summary>
    Staged,

    /// <summary>Modified but unstaged.</summary>
    Unstaged,

    /// <summary>Untracked.</summary>
    Untracked,
}
