namespace Novolis.IO.Git;

/// <summary>Kind of Git worktree represented by a repository workspace.</summary>
public enum GitWorktreeKind
{
    Main = 0,
    Linked = 1,
    Bare = 2,
    Unknown = 3,
}
