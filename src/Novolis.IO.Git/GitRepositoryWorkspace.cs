using System.IO.Abstractions;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;

namespace Novolis.IO.Git;

/// <summary>A Git repository identity at one typed directory root.</summary>
public sealed class GitRepositoryWorkspace : RootWorkspace
{
    /// <summary>Creates identity for one git checkout.</summary>
    public GitRepositoryWorkspace(IDirectoryInfo root, string repositoryName, GitWorktreeKind worktreeKind = GitWorktreeKind.Unknown)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryName);

        Root = root;
        RepositoryName = repositoryName;
        WorktreeKind = worktreeKind;
    }

    /// <inheritdoc />
    public IDirectoryInfo Root { get; }

    /// <summary>Folder name of the checkout.</summary>
    public string RepositoryName { get; }

    /// <summary>Whether <c>.git</c> is a main worktree, a linked worktree, or unknown.</summary>
    public GitWorktreeKind WorktreeKind { get; }

    /// <summary>Opens identity for an existing checkout directory.</summary>
    public static GitRepositoryWorkspace Open(string path, IFileSystem? fileSystem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        fileSystem ??= new FileSystem();
        var root = fileSystem.DirectoryInfo.New(Path.GetFullPath(path));
        return new GitRepositoryWorkspace(root, root.Name, DetectWorktreeKind(fileSystem, root.FullName));
    }

    /// <summary>Classifies a checkout from whether <c>.git</c> is a file or a directory.</summary>
    public static GitWorktreeKind DetectWorktreeKind(string repositoryRoot, IFileSystem? fileSystem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        fileSystem ??= new FileSystem();
        return DetectWorktreeKind(fileSystem, Path.GetFullPath(repositoryRoot));
    }

    internal static GitWorktreeKind DetectWorktreeKind(IFileSystem fileSystem, string repositoryRoot)
    {
        var gitPath = fileSystem.Path.Combine(repositoryRoot, ".git");
        if (fileSystem.File.Exists(gitPath))
            return GitWorktreeKind.Linked;
        if (fileSystem.Directory.Exists(gitPath))
            return GitWorktreeKind.Main;
        return GitWorktreeKind.Unknown;
    }
}
