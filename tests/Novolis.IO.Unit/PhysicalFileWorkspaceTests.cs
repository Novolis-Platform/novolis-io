using Novolis.IO.Workspace;

namespace Novolis.IO.Unit;

public sealed class PhysicalFileWorkspaceTests
{
    [Test]
    public async Task Physical_workspace_round_trip_on_disk()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-physical-");
        try
        {
            var ws = new PhysicalFileWorkspace(temp.FullName);
            ws.WriteAllText(Path.Combine(temp.FullName, "note.txt"), "hello");
            await Assert.That(ws.FileExists(Path.Combine(temp.FullName, "note.txt"))).IsTrue();
            await Assert.That(await ws.ReadAllTextAsync(Path.Combine(temp.FullName, "note.txt"))).IsEqualTo("hello");

            var files = ws.EnumerateFiles(temp.FullName, "*.txt").ToList();
            await Assert.That(files.Count).IsEqualTo(1);

            ws.DeleteFile(files[0]);
            await Assert.That(ws.FileExists(files[0])).IsFalse();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Static_disk_probes()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-static-");
        try
        {
            var path = Path.Combine(temp.FullName, "probe.txt");
            await File.WriteAllTextAsync(path, "x");
            await Assert.That(PhysicalFileWorkspace.FileExistsOnDisk(path)).IsTrue();
            await Assert.That(PhysicalFileWorkspace.DirectoryExistsOnDisk(temp.FullName)).IsTrue();
            await Assert.That(PhysicalFileWorkspace.ReadAllTextOnDisk(path)).IsEqualTo("x");
            var listed = PhysicalFileWorkspace.GetFiles(temp.FullName, "*.txt", SearchOption.TopDirectoryOnly);
            await Assert.That(listed.Length).IsEqualTo(1);
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
