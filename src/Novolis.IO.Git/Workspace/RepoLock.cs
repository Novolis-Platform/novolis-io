namespace Novolis.IO.Git;

/// <summary>Simple file lock under workspace <c>.novolis/locks/</c>.</summary>
public sealed class RepoLock : IDisposable
{
    readonly string _path;
    readonly FileStream _stream;

    RepoLock(string path, FileStream stream)
    {
        _path = path;
        _stream = stream;
    }

    /// <summary>Acquires an exclusive lock for mutate ops (pull/branch).</summary>
    public static RepoLock? TryAcquireExclusive(string workspaceRoot, string repoName, TimeSpan? timeout = null)
    {
        var dir = Path.Combine(workspaceRoot, ".novolis", "locks");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Sanitize(repoName) + ".lock");
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            try
            {
                var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new RepoLock(path, fs);
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= deadline)
                    return null;
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>Acquires a shared lock for fetch (multiple readers).</summary>
    public static RepoLock? TryAcquireShared(string workspaceRoot, string repoName, TimeSpan? timeout = null)
    {
        var dir = Path.Combine(workspaceRoot, ".novolis", "locks");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Sanitize(repoName) + ".lock");
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            try
            {
                var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
                return new RepoLock(path, fs);
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= deadline)
                    return null;
                Thread.Sleep(50);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stream.Dispose();
        try
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
        catch
        {
            // best-effort
        }
    }

    static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
