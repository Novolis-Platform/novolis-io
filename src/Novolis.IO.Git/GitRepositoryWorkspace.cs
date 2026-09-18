using System.IO.Abstractions;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;

namespace Novolis.IO.Git;

/// <summary>Kind of Git worktree represented by a repository workspace.</summary>
public enum GitWorktreeKind
{
    Main = 0,
    Linked = 1,
    Bare = 2,
    Unknown = 3,
}

/// <summary>A Git repository identity at one typed directory root.</summary>
public sealed class GitRepositoryWorkspace : RootWorkspace
{
    public GitRepositoryWorkspace(IDirectoryInfo root, string repositoryName, GitWorktreeKind worktreeKind = GitWorktreeKind.Unknown)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);

        Root = root;
        RepositoryName = repositoryName;
        WorktreeKind = worktreeKind;
    }

    public IDirectoryInfo Root { get; }
    public string RepositoryName { get; }
    public GitWorktreeKind WorktreeKind { get; }
}

/// <summary>An arbitrary collection of Git repository workspaces with no shared-root guarantee.</summary>
public sealed record GitRepositoryWorkspaceSet(IReadOnlyList<GitRepositoryWorkspace> Members);

/// <summary>A rooted collection of Git repository workspaces.</summary>
public sealed class MultiGitRepositoryWorkspace : RootWorkspace
{
    public MultiGitRepositoryWorkspace(IDirectoryInfo root, IReadOnlyList<GitRepositoryWorkspace> members)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(members);

        Root = root;
        Members = members;
        ValidateMembers(root, members);
    }

    public IDirectoryInfo Root { get; }
    public IReadOnlyList<GitRepositoryWorkspace> Members { get; }

    private static void ValidateMembers(IDirectoryInfo root, IReadOnlyList<GitRepositoryWorkspace> members)
    {
        var rootPath = EnsureTrailingSeparator(root.FullName);
        foreach (var member in members)
        {
            var memberPath = member.Root.FullName;
            if (!memberPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(memberPath, root.FullName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Repository workspace '{member.RepositoryName}' is outside rooted group '{root.FullName}'.",
                    nameof(members));
            }
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;
}
