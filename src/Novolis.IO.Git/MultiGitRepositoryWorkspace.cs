using System.IO.Abstractions;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;

namespace Novolis.IO.Git;

/// <summary>A rooted collection of Git repository workspaces.</summary>
public sealed class MultiGitRepositoryWorkspace : RootWorkspace
{
    /// <summary>Creates a rooted group. Every member must live under <paramref name="root"/>.</summary>
    public MultiGitRepositoryWorkspace(IDirectoryInfo root, IReadOnlyList<GitRepositoryWorkspace> members)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(members);

        Root = root;
        Members = members;
        ValidateMembers(root, members);
    }

    /// <inheritdoc />
    public IDirectoryInfo Root { get; }

    /// <summary>Git checkouts under <see cref="Root"/>.</summary>
    public IReadOnlyList<GitRepositoryWorkspace> Members { get; }

    /// <summary>Discovers members under a path.</summary>
    public static MultiGitRepositoryWorkspace Discover(
        string root,
        GitDiscover policy = GitDiscover.GitChildren,
        IFileSystem? fileSystem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        fileSystem ??= new FileSystem();
        var directory = fileSystem.DirectoryInfo.New(Path.GetFullPath(root));
        return Discover(directory, policy);
    }

    /// <summary>Discovers members under an I/O workspace root.</summary>
    public static MultiGitRepositoryWorkspace Discover(
        RootWorkspace workspace,
        GitDiscover policy = GitDiscover.GitChildren)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return Discover(workspace.Root, policy);
    }

    /// <summary>Discovers members under a directory root.</summary>
    public static MultiGitRepositoryWorkspace Discover(
        IDirectoryInfo root,
        GitDiscover policy = GitDiscover.GitChildren)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!root.Exists)
            throw new DirectoryNotFoundException(root.FullName);

        var fileSystem = root.FileSystem;
        var search = policy == GitDiscover.NovolisPrefix ? "novolis-*" : "*";
        var members = root.EnumerateDirectories(search)
            .Where(dir => HasGit(fileSystem, dir.FullName))
            .Select(dir => new GitRepositoryWorkspace(
                dir,
                dir.Name,
                GitRepositoryWorkspace.DetectWorktreeKind(fileSystem, dir.FullName)))
            .OrderBy(member => member.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MultiGitRepositoryWorkspace(root, members);
    }

    /// <summary>Returns a forest with the same root and name-filtered members.</summary>
    public MultiGitRepositoryWorkspace Select(RepoFilter? filter)
    {
        return new MultiGitRepositoryWorkspace(Root, SelectMembers(Members, filter));
    }

    /// <summary>Applies include/exclude names. Status flags on <paramref name="filter"/> are ignored.</summary>
    public static IReadOnlyList<GitRepositoryWorkspace> SelectMembers(
        IReadOnlyList<GitRepositoryWorkspace> members,
        RepoFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(members);
        filter ??= new RepoFilter();
        IEnumerable<GitRepositoryWorkspace> query = members;
        if (filter.Include is { Count: > 0 })
        {
            var set = NormalizeNames(filter.Include);
            query = query.Where(member =>
                set.Contains(member.RepositoryName) || set.Contains(StripPrefix(member.RepositoryName)));
        }

        if (filter.Exclude is { Count: > 0 })
        {
            var set = NormalizeNames(filter.Exclude);
            query = query.Where(member =>
                !set.Contains(member.RepositoryName) && !set.Contains(StripPrefix(member.RepositoryName)));
        }

        return query.ToArray();
    }

    static bool HasGit(IFileSystem fileSystem, string directory)
    {
        var gitPath = fileSystem.Path.Combine(directory, ".git");
        return fileSystem.Directory.Exists(gitPath) || fileSystem.File.Exists(gitPath);
    }

    static HashSet<string> NormalizeNames(IEnumerable<string> names)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            foreach (var part in name.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                set.Add(part);
                set.Add(part.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase)
                    ? part
                    : "novolis-" + part);
                set.Add(StripPrefix(part));
            }
        }

        return set;
    }

    static string StripPrefix(string name) =>
        name.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase) ? name["novolis-".Length..] : name;

    static void ValidateMembers(IDirectoryInfo root, IReadOnlyList<GitRepositoryWorkspace> members)
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

    static string EnsureTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;
}
