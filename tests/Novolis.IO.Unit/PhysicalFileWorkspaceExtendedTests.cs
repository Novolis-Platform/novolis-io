using Microsoft.Extensions.FileProviders;
using Novolis.IO.Processes;
using Novolis.IO.Workspace;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class PhysicalFileWorkspaceExtendedTests
{
    [Test]
    public async Task Workspace_root_is_a_directory_info()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-root-");
        try
        {
            IWorkspace workspace = new PhysicalFileWorkspace(temp.FullName);
            await Assert.That(workspace.Root.Exists).IsTrue();
            await Assert.That(workspace.Root.FullName).IsEqualTo(temp.FullName);
            ((IDisposable)workspace).Dispose();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Read_write_bytes_and_append()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-physical-ext-");
        try
        {
            var ws = new PhysicalFileWorkspace(temp.FullName);
            var path = Path.Combine(temp.FullName, "data.bin");
            ws.WriteAllBytes(path, [9, 8, 7]);
            var read = ws.ReadAllBytes(path);
            await Assert.That(read.Length).IsEqualTo(3);
            await Assert.That(read[0]).IsEqualTo((byte)9);
            await ws.AppendAllTextAsync(path, "tail");
            await Assert.That(await ws.ReadAllTextAsync(path)).Contains("tail");

            await using var stream = ws.CreateFileStream(
                Path.Combine(temp.FullName, "stream.txt"),
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("streamed");
            ws.Dispose();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Static_enumeration_and_current_directory()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-static-ext-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(temp.FullName, "one.txt"), "1");
            await File.WriteAllTextAsync(Path.Combine(temp.FullName, "two.txt"), "2");
            PhysicalFileWorkspace.CreateDirectoryOnDisk(Path.Combine(temp.FullName, "nested"));
            var files = PhysicalFileWorkspace.GetFiles(temp.FullName, "*.txt", SearchOption.TopDirectoryOnly);
            await Assert.That(files.Length).IsEqualTo(2);
            var enumerated = PhysicalFileWorkspace.EnumerateFiles(temp.FullName, "*.txt", SearchOption.TopDirectoryOnly).ToList();
            await Assert.That(enumerated.Count).IsEqualTo(2);
            await Assert.That(PhysicalFileWorkspace.GetCurrentDirectory()).IsNotNull();
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
