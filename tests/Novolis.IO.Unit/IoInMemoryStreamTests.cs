using Novolis.IO.Processes;
using Novolis.IO.Workspace;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class IoInMemoryStreamTests
{
    [Test]
    public async Task InMemoryFileWorkspace_stream_edge_cases()
    {
        var ws = new InMemoryFileWorkspace(@"C:\edge");
        ws.WriteAllText(@"C:\edge\exists.txt", "old");
        await Assert.That(() => ws.CreateFileStream(
                @"C:\edge\exists.txt",
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.None))
            .Throws<IOException>();

        await using (var truncate = ws.CreateFileStream(
                         @"C:\edge\exists.txt",
                         FileMode.Truncate,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.None))
        {
            await truncate.WriteAsync("new"u8.ToArray());
        }

        await Assert.That(await ws.ReadAllTextAsync(@"C:\edge\exists.txt")).IsEqualTo("new");

        await Assert.That(() => ws.CreateFileStream(
                @"C:\edge\missing.txt",
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.None))
            .Throws<FileNotFoundException>();

        await ws.AppendAllTextAsync(@"C:\edge\exists.txt", "!");
        await Assert.That(await ws.ReadAllTextAsync(@"C:\edge\exists.txt")).IsEqualTo("new!");

        var bytes = ws.ReadAllBytes(@"C:\edge\exists.txt");
        await Assert.That(bytes.Length).IsGreaterThan(0);

        ws.EnsureDirectoryExists(@"C:\edge\nested\dir");
        ws.WriteAllText(@"C:\edge\nested\dir\leaf.txt", "leaf");
        var entries = ws.EnumerateFileSystemEntries(@"C:\edge\nested").ToList();
        await Assert.That(entries.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task InMemoryFileWorkspace_provider_missing_paths()
    {
        var ws = new InMemoryFileWorkspace(@"C:\prov");
        var missingFile = ws.Provider.GetFileInfo("nope.txt");
        await Assert.That(missingFile.Exists).IsFalse();
        var missingDir = ws.Provider.GetDirectoryContents("missing");
        await Assert.That(missingDir.Exists).IsFalse();
    }

    [Test]
    public async Task PhysicalFileWorkspace_move_and_missing_read()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-phys-move-");
        try
        {
            var ws = new PhysicalFileWorkspace(temp.FullName);
            var src = Path.Combine(temp.FullName, "src.txt");
            var dst = Path.Combine(temp.FullName, "dst.txt");
            ws.WriteAllText(src, "payload");
            ws.MoveFile(src, dst, overwrite: false);
            await Assert.That(ws.FileExists(dst)).IsTrue();
            await Assert.That(() => ws.ReadAllBytes(Path.Combine(temp.FullName, "missing.bin")))
                .Throws<FileNotFoundException>();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task ProcessTree_kills_child_process_tree()
    {
        if (!OperatingSystem.IsWindows())
        {
            ProcessTree.Kill(999_999);
            return;
        }

        using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c start /b cmd /c timeout /t 120 /nobreak >nul",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        await Task.Delay(500);
        ProcessTree.Kill(proc.Id);
        await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(proc.HasExited).IsTrue();
    }
}
