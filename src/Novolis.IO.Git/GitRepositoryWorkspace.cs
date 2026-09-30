using System.IO.Abstractions;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;

namespace Novolis.IO.Git;

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
