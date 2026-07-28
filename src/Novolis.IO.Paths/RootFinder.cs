namespace Novolis.IO.Paths;

/// <summary>Walks parent directories to find a workspace root.</summary>
public static class RootFinder
{
    /// <summary>Finds the nearest ancestor matching <paramref name="isRoot"/>.</summary>
    public static bool TryFind(string startDir, Func<DirectoryInfo, bool> isRoot, out string root)
    {
        ArgumentNullException.ThrowIfNull(isRoot);
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (isRoot(dir))
            {
                root = dir.FullName;
                return true;
            }

            dir = dir.Parent;
        }

        root = startDir;
        return false;
    }

    /// <summary>Finds a root where all <paramref name="requiredRelativePaths"/> exist.</summary>
    public static bool TryFind(string startDir, IReadOnlyList<string> requiredRelativePaths, out string root)
    {
        ArgumentNullException.ThrowIfNull(requiredRelativePaths);
        return TryFind(startDir, dir =>
        {
            foreach (var rel in requiredRelativePaths)
            {
                var path = Path.Combine(dir.FullName, rel);
                if (!File.Exists(path) && !Directory.Exists(path))
                    return false;
            }

            return requiredRelativePaths.Count > 0;
        }, out root);
    }
}
