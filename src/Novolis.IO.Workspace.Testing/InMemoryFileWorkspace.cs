using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Novolis.IO.Workspace.Testing;

/// <summary>Volatile <see cref="IFileWorkspace"/> for tests (no disk IO).</summary>
public sealed class InMemoryFileWorkspace : IFileWorkspace
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly InMemoryFileProvider _provider;
    private bool _disposed;

    public InMemoryFileWorkspace(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = NormalizePath(rootPath);
        _provider = new InMemoryFileProvider(this);
        TouchDirectory(RootPath);
    }

    public IFileProvider Provider => _provider;
    public string RootPath { get; }

    public void EnsureDirectoryExists(string directoryPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TouchDirectory(NormalizePath(directoryPath));
    }

    public bool DirectoryExists(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var d = NormalizePath(path);
        if (_directories.ContainsKey(d))
            return true;
        var prefix = WithTrailingSeparator(d);
        return _files.Keys.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public bool FileExists(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _files.ContainsKey(NormalizePath(path));
    }

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var dir = WithTrailingSeparator(NormalizePath(directoryPath));
        foreach (var key in _files.Keys)
        {
            if (!key.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                continue;
            var name = Path.GetFileName(key);
            if (MatchesSimplePattern(name, searchPattern))
                yield return key;
        }
    }

    public IEnumerable<string> EnumerateFileSystemEntries(string directoryPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var dir = WithTrailingSeparator(NormalizePath(directoryPath));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _files.Keys)
        {
            if (!key.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                continue;
            var rel = key[dir.Length..];
            var slash = rel.IndexOf(Separator);
            var name = slash < 0 ? rel : rel[..slash];
            if (string.IsNullOrEmpty(name))
                continue;
            var full = CombineVirtual(dir.TrimEnd(Separator), name);
            if (seen.Add(full))
                yield return full;
        }

        foreach (var d in _directories.Keys)
        {
            if (!d.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                continue;
            var rel = d[dir.Length..];
            var slash = rel.IndexOf(Separator);
            var name = slash < 0 ? rel : rel[..slash];
            if (string.IsNullOrEmpty(name))
                continue;
            var full = CombineVirtual(dir.TrimEnd(Separator), name);
            if (seen.Add(full))
                yield return full;
        }
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var p = NormalizePath(path);
        if (!_files.TryGetValue(p, out var bytes))
            throw new FileNotFoundException(null, p);
        return Task.FromResult(Encoding.UTF8.GetString(bytes));
    }

    public byte[] ReadAllBytes(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var p = NormalizePath(path);
        if (!_files.TryGetValue(p, out var bytes))
            throw new FileNotFoundException(null, p);
        return bytes;
    }

    public void WriteAllBytes(string path, byte[] contents)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var p = NormalizePath(path);
        TouchDirectory(GetDirectoryName(p)!);
        _files[p] = contents;
    }

    public void WriteAllText(string path, string contents)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var p = NormalizePath(path);
        TouchDirectory(GetDirectoryName(p)!);
        _files[p] = Encoding.UTF8.GetBytes(contents);
    }

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        WriteAllText(path, contents);
        return Task.CompletedTask;
    }

    public Task AppendAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var p = NormalizePath(path);
        TouchDirectory(GetDirectoryName(p)!);
        _files.AddOrUpdate(
            p,
            _ => Encoding.UTF8.GetBytes(contents),
            (_, old) =>
            {
                var tail = Encoding.UTF8.GetBytes(contents);
                var merged = new byte[old.Length + tail.Length];
                Buffer.BlockCopy(old, 0, merged, 0, old.Length);
                Buffer.BlockCopy(tail, 0, merged, old.Length, tail.Length);
                return merged;
            });
        return Task.CompletedTask;
    }

    public Stream CreateFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var p = NormalizePath(path);
        if (access == FileAccess.Read)
        {
            if (mode != FileMode.Open)
                throw new NotSupportedException($"In-memory workspace: read requires FileMode.Open (got {mode}).");
            if (!_files.TryGetValue(p, out var bytes))
                throw new FileNotFoundException(null, p);
            return new MemoryStream(bytes, writable: false);
        }

        if (access is FileAccess.Write or FileAccess.ReadWrite)
        {
            TouchDirectory(GetDirectoryName(p)!);
            if (mode is FileMode.Create or FileMode.CreateNew)
            {
                if (mode == FileMode.CreateNew && _files.ContainsKey(p))
                    throw new IOException("File exists");
                return new CommittingWriteStream(this, p, append: false);
            }

            if (mode == FileMode.Append)
                return new CommittingWriteStream(this, p, append: true);

            if (mode == FileMode.Truncate)
                return new CommittingWriteStream(this, p, append: false);

            if (mode == FileMode.OpenOrCreate)
                return new CommittingWriteStream(this, p, append: false);

            if (mode == FileMode.Open)
            {
                if (!_files.ContainsKey(p))
                    throw new FileNotFoundException(null, p);
                return new CommittingWriteStream(this, p, append: false);
            }
        }

        throw new NotSupportedException($"In-memory workspace: unsupported mode={mode}, access={access}.");
    }

    public void DeleteFile(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _files.TryRemove(NormalizePath(path), out _);
    }

    public void MoveFile(string sourceFileName, string destFileName, bool overwrite)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var s = NormalizePath(sourceFileName);
        var d = NormalizePath(destFileName);
        if (!_files.TryGetValue(s, out var bytes))
            throw new FileNotFoundException(null, s);
        if (!overwrite && _files.ContainsKey(d))
            throw new IOException("Destination exists");
        _files.TryRemove(s, out _);
        TouchDirectory(GetDirectoryName(d)!);
        _files[d] = bytes;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
    }

    internal byte[]? TryGetBytes(string fullPath) =>
        _files.TryGetValue(NormalizePath(fullPath), out var b) ? b : null;

    internal void SetBytes(string fullPath, byte[] bytes)
    {
        var p = NormalizePath(fullPath);
        TouchDirectory(GetDirectoryName(p)!);
        _files[p] = bytes;
    }

    private void TouchDirectory(string directoryPath)
    {
        var d = NormalizePath(directoryPath);
        _directories[d] = 0;
        var parent = GetDirectoryName(d);
        if (!string.IsNullOrEmpty(parent) && !string.Equals(parent, d, StringComparison.OrdinalIgnoreCase))
            TouchDirectory(parent);
    }

    // Canonical virtual separator. Windows drive roots (e.g. C:\mem-root) stay absolute
    // on Linux; Path.GetFullPath would treat '\' as a filename character there.
    private const char Separator = '/';

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var unified = path.Replace('\\', Separator);

        if (!IsVirtuallyRooted(unified))
            unified = Path.GetFullPath(path).Replace('\\', Separator);

        return CollapseSegments(unified);
    }

    private static bool IsVirtuallyRooted(string path) =>
        path.StartsWith(Separator) ||
        (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':');

    private static string CollapseSegments(string path)
    {
        string root;
        string remainder;

        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            root = path[..2];
            remainder = path.Length > 2 ? path[2..] : string.Empty;
            if (remainder.StartsWith(Separator))
                remainder = remainder[1..];
        }
        else if (path.StartsWith(Separator))
        {
            root = Separator.ToString();
            remainder = path[1..];
        }
        else
        {
            root = string.Empty;
            remainder = path;
        }

        var parts = new List<string>();
        foreach (var segment in remainder.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(segment);
        }

        if (root == Separator.ToString())
            return parts.Count == 0 ? root : root + string.Join(Separator, parts);

        if (root.Length == 2 && root[1] == ':')
            return parts.Count == 0 ? root + Separator : root + Separator + string.Join(Separator, parts);

        return parts.Count == 0 ? string.Empty : string.Join(Separator, parts);
    }

    private static string? GetDirectoryName(string path)
    {
        var n = NormalizePath(path);
        if (n.Length >= 2 && char.IsAsciiLetter(n[0]) && n[1] == ':' && (n.Length == 2 || (n.Length == 3 && n[2] == Separator)))
            return null;
        if (n == Separator.ToString())
            return null;

        var idx = n.LastIndexOf(Separator);
        if (idx < 0)
            return null;
        if (idx == 0)
            return Separator.ToString();
        // "C:/foo" → "C:/"
        if (idx == 2 && char.IsAsciiLetter(n[0]) && n[1] == ':')
            return n[..3];
        return n[..idx];
    }

    private static string CombineVirtual(string basePath, string relative)
    {
        if (string.IsNullOrEmpty(relative))
            return NormalizePath(basePath);
        var rel = relative.Replace('\\', Separator).Trim(Separator);
        var b = NormalizePath(basePath).TrimEnd(Separator);
        return NormalizePath(string.IsNullOrEmpty(rel) ? b : b + Separator + rel);
    }

    private static string WithTrailingSeparator(string directoryPath)
    {
        if (directoryPath.EndsWith(Separator))
            return directoryPath;
        return directoryPath + Separator;
    }

    private static bool MatchesSimplePattern(string fileName, string searchPattern)
    {
        if (searchPattern == "*")
            return true;
        if (searchPattern.StartsWith('*') && searchPattern.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        return fileName.Equals(searchPattern, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CommittingWriteStream : MemoryStream
    {
        private readonly InMemoryFileWorkspace _owner;
        private readonly string _path;
        private readonly bool _append;
        private bool _committed;

        public CommittingWriteStream(InMemoryFileWorkspace owner, string path, bool append) : base()
        {
            _owner = owner;
            _path = path;
            _append = append;
            if (append && owner._files.TryGetValue(path, out var existing))
                Write(existing, 0, existing.Length);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_committed)
            {
                _committed = true;
                var buffer = ToArray();
                _owner.SetBytes(_path, buffer);
            }

            base.Dispose(disposing);
        }
    }

    private sealed class InMemoryFileProvider : IFileProvider
    {
        private readonly InMemoryFileWorkspace _owner;

        public InMemoryFileProvider(InMemoryFileWorkspace owner) => _owner = owner;

        public IFileInfo GetFileInfo(string subpath)
        {
            var full = string.IsNullOrEmpty(subpath)
                ? _owner.RootPath
                : CombineVirtual(_owner.RootPath, subpath);
            return new InMemoryFileInfo(_owner, full);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var full = string.IsNullOrEmpty(subpath)
                ? _owner.RootPath
                : CombineVirtual(_owner.RootPath, subpath);
            return new InMemoryDirectoryContents(_owner, full);
        }

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class InMemoryFileInfo : IFileInfo
    {
        private readonly InMemoryFileWorkspace _owner;
        private readonly string _fullPath;

        public InMemoryFileInfo(InMemoryFileWorkspace owner, string fullPath)
        {
            _owner = owner;
            _fullPath = fullPath;
            var bytes = _owner.TryGetBytes(fullPath);
            Exists = bytes != null || _owner._directories.ContainsKey(fullPath);
            IsDirectory = _owner._directories.ContainsKey(fullPath) && bytes == null;
            Length = bytes?.Length ?? 0;
            PhysicalPath = fullPath;
            Name = Path.GetFileName(fullPath.TrimEnd(Separator));
            LastModified = DateTimeOffset.UtcNow;
        }

        public bool Exists { get; }
        public long Length { get; }
        public string PhysicalPath { get; }
        public string Name { get; }
        public DateTimeOffset LastModified { get; }
        public bool IsDirectory { get; }

        public Stream CreateReadStream()
        {
            var bytes = _owner.TryGetBytes(_fullPath) ?? throw new FileNotFoundException(null, _fullPath);
            return new MemoryStream(bytes, writable: false);
        }
    }

    private sealed class InMemoryDirectoryContents : IDirectoryContents
    {
        private readonly InMemoryFileWorkspace _owner;
        private readonly string _fullPath;

        public InMemoryDirectoryContents(InMemoryFileWorkspace owner, string fullPath)
        {
            _owner = owner;
            _fullPath = fullPath;
            Exists = owner.DirectoryExists(fullPath);
        }

        public bool Exists { get; }

        public IEnumerator<IFileInfo> GetEnumerator() =>
            _owner.EnumerateFileSystemEntries(_fullPath).Select(p => new InMemoryFileInfo(_owner, p)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
