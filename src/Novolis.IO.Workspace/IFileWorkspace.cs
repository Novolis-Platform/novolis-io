using Microsoft.Extensions.FileProviders;

namespace Novolis.IO.Workspace;

/// <summary>Process-local file access for a single root: <see cref="IFileProvider"/> for reads and explicit write/delete helpers (M.E. has no write API on <see cref="IFileInfo"/> yet).</summary>
public interface IFileWorkspace : IDisposable
{
    IFileProvider Provider { get; }
    string RootPath { get; }

    void EnsureDirectoryExists(string directoryPath);
    bool DirectoryExists(string path);
    bool FileExists(string path);
    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern);
    IEnumerable<string> EnumerateFileSystemEntries(string directoryPath);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    byte[] ReadAllBytes(string path);
    void WriteAllBytes(string path, byte[] contents);
    void WriteAllText(string path, string contents);
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    Task AppendAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    Stream CreateFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options);
    void DeleteFile(string path);
    void MoveFile(string sourceFileName, string destFileName, bool overwrite);
}
