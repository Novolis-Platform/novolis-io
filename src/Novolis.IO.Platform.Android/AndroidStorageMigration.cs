namespace Novolis.IO.Platform.Android;

/// <summary>Moves an older product tree onto the resolved root.</summary>
public static class AndroidStorageMigration
{
    /// <summary>True when <paramref name="directory"/> exists and contains any entry.</summary>
    public static bool HasPayload(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return false;

        return Directory.EnumerateFileSystemEntries(directory).Any();
    }

    /// <summary>
    /// Moves <paramref name="legacyDirectory"/> onto <paramref name="destinationDirectory"/>
    /// when the destination has no files yet. Same-volume rename is used first; a copy is
    /// used when the trees sit on different volumes.
    /// </summary>
    public static void MoveLegacyTree(string legacyDirectory, string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(legacyDirectory) || string.IsNullOrWhiteSpace(destinationDirectory))
            return;

        var source = Path.GetFullPath(legacyDirectory);
        var destination = Path.GetFullPath(destinationDirectory);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            return;

        if (!Directory.Exists(source) || !HasPayload(source))
            return;

        if (Directory.Exists(destination) && HasPayload(destination))
            return;

        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: false);

        try
        {
            Directory.Move(source, destination);
        }
        catch (IOException)
        {
            CopyDirectory(source, destination);
            Directory.Delete(source, recursive: true);
        }
    }

    static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var name = Path.GetFileName(entry);
            if (string.IsNullOrEmpty(name))
                continue;

            var target = Path.Combine(destination, name);
            if (Directory.Exists(entry))
                CopyDirectory(entry, target);
            else
                File.Copy(entry, target, overwrite: false);
        }
    }
}
