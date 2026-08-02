using Microsoft.Extensions.FileProviders;
using Novolis.IO.Processes;
using Novolis.IO.Workspace;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class InMemoryFileWorkspaceExtendedTests
{
    [Test]
    public async Task Provider_exposes_files_and_directories()
    {
        var ws = new InMemoryFileWorkspace(@"C:\mem-root");
        ws.WriteAllText(@"C:\mem-root\docs\readme.md", "# hi");
        ws.EnsureDirectoryExists(@"C:\mem-root\empty");

        var fileInfo = ws.Provider.GetFileInfo("docs/readme.md");
        await Assert.That(fileInfo.Exists).IsTrue();
        await Assert.That(fileInfo.Length).IsGreaterThan(0);
        await using (var stream = fileInfo.CreateReadStream())
        {
            using var reader = new StreamReader(stream);
            await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("# hi");
        }

        var dirContents = ws.Provider.GetDirectoryContents("docs");
        await Assert.That(dirContents.Exists).IsTrue();
        await Assert.That(dirContents.ToList().Count).IsGreaterThanOrEqualTo(1);

        var missingDir = ws.Provider.GetDirectoryContents("missing");
        await Assert.That(missingDir.Exists).IsFalse();
    }

    [Test]
    public async Task CreateFileStream_write_modes_and_read()
    {
        var ws = new InMemoryFileWorkspace(@"C:\stream-root");
        await using (var create = ws.CreateFileStream(
                         @"C:\stream-root\new.bin",
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.None))
        {
            await create.WriteAsync(new byte[] { 1, 2, 3 });
        }

        await Assert.That(ws.FileExists(@"C:\stream-root\new.bin")).IsTrue();
        var bytes = ws.ReadAllBytes(@"C:\stream-root\new.bin");
        await Assert.That(bytes.Length).IsEqualTo(3);
        await Assert.That(bytes[0]).IsEqualTo((byte)1);

        await using (var append = ws.CreateFileStream(
                         @"C:\stream-root\new.bin",
                         FileMode.Append,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.None))
        {
            await append.WriteAsync(new byte[] { 4 });
        }

        var appended = ws.ReadAllBytes(@"C:\stream-root\new.bin");
        await Assert.That(appended.Length).IsEqualTo(4);
        await Assert.That(appended[3]).IsEqualTo((byte)4);

        await using var read = ws.CreateFileStream(
            @"C:\stream-root\new.bin",
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.None);
        await Assert.That(read.Length).IsEqualTo(4);
    }

    [Test]
    public async Task EnumerateFiles_supports_wildcard_json_pattern()
    {
        var ws = new InMemoryFileWorkspace(@"C:\glob");
        ws.WriteAllText(@"C:\glob\a.json", "{}");
        ws.WriteAllText(@"C:\glob\b.txt", "x");
        var jsonFiles = ws.EnumerateFiles(@"C:\glob", "*.json").ToList();
        await Assert.That(jsonFiles.Count).IsEqualTo(1);
        await Assert.That(jsonFiles[0]).EndsWith("a.json");
    }

    [Test]
    public async Task Dispose_blocks_further_access()
    {
        var ws = new InMemoryFileWorkspace(@"C:\dispose");
        ws.Dispose();
        await Assert.That(() => ws.FileExists(@"C:\dispose\x")).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task MoveFile_without_overwrite_throws_when_destination_exists()
    {
        var ws = new InMemoryFileWorkspace(@"C:\move");
        ws.WriteAllText(@"C:\move\a.txt", "a");
        ws.WriteAllText(@"C:\move\b.txt", "b");
        await Assert.That(() => ws.MoveFile(@"C:\move\a.txt", @"C:\move\b.txt", overwrite: false))
            .Throws<IOException>();
    }
}

public sealed class PhysicalFileWorkspaceExtendedTests
{
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

public sealed class ProcessTreeTests
{
    [Test]
    public async Task Kill_noops_for_invalid_pid()
    {
        ProcessTree.Kill(0);
        ProcessTree.Kill(-1);
        var queue = new ProcessJobQueue();
        await Assert.That(queue.MaxParallel).IsEqualTo(2);
    }
}
