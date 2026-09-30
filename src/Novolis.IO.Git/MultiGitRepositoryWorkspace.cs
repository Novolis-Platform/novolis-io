using System.IO.Abstractions;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;

namespace Novolis.IO.Git;

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
