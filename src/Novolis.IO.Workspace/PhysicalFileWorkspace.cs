using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Novolis.IO.Workspace;

/// <summary>Disk-backed <see cref="IFileWorkspace"/>; all <see cref="System.IO.File"/> / <see cref="System.IO.Directory"/> usage is confined here.</summary>
public sealed class PhysicalFileWorkspace : IFileWorkspace
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Existence probe without creating a workspace root (for layout discovery where <see cref="PhysicalFileWorkspace"/> construction would create directories).</summary>
    public static bool FileExistsOnDisk(string path) => File.Exists(Path.GetFullPath(path));

    /// <summary>Directory existence probe without creating a workspace root.</summary>
    public static bool DirectoryExistsOnDisk(string path) => Directory.Exists(Path.GetFullPath(path));

    public static void CreateDirectoryOnDisk(string path) => Directory.CreateDirectory(path);

    public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(Path.GetFullPath(path), searchPattern, searchOption);

    public static IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(Path.GetFullPath(path), searchPattern, searchOption);

    public static string GetCurrentDirectory() => Directory.GetCurrentDirectory();

    public static string ReadAllTextOnDisk(string path) => File.ReadAllText(Path.GetFullPath(path), Utf8NoBom);

    private readonly PhysicalFileProvider _provider;
    private bool _disposed;

    public PhysicalFileWorkspace(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(RootPath);
        _provider = new PhysicalFileProvider(RootPath, ExclusionFilters.None);
    }

    public IFileProvider Provider => _provider;
    public string RootPath { get; }

    public void EnsureDirectoryExists(string directoryPath) =>
        Directory.CreateDirectory(Path.GetFullPath(directoryPath));

    public bool DirectoryExists(string path) =>
        Directory.Exists(Path.GetFullPath(path));

    public bool FileExists(string path) =>
        File.Exists(Path.GetFullPath(path));

    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern) =>
        Directory.EnumerateFiles(Path.GetFullPath(directoryPath), searchPattern);

    public IEnumerable<string> EnumerateFileSystemEntries(string directoryPath) =>
        Directory.EnumerateFileSystemEntries(Path.GetFullPath(directoryPath));

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken);

    public byte[] ReadAllBytes(string path) =>
        File.ReadAllBytes(Path.GetFullPath(path));

    public void WriteAllBytes(string path, byte[] contents) =>
        File.WriteAllBytes(Path.GetFullPath(path), contents);

    public void WriteAllText(string path, string contents) =>
        File.WriteAllText(Path.GetFullPath(path), contents, Utf8NoBom);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(Path.GetFullPath(path), contents, Utf8NoBom, cancellationToken);

    public Task AppendAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) =>
        File.AppendAllTextAsync(Path.GetFullPath(path), contents, cancellationToken);

    public Stream CreateFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) =>
        new FileStream(Path.GetFullPath(path), mode, access, share, bufferSize, options);

    public void DeleteFile(string path) =>
        File.Delete(Path.GetFullPath(path));

    public void MoveFile(string sourceFileName, string destFileName, bool overwrite) =>
        File.Move(Path.GetFullPath(sourceFileName), Path.GetFullPath(destFileName), overwrite);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _provider.Dispose();
    }
}
