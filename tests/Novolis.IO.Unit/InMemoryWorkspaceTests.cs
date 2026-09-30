using Novolis.IO.Paths;
using Novolis.IO.Recovery;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class InMemoryWorkspaceTests
{
    [Test]
    public async Task EnumerateFiles_matches_glob_patterns()
    {
        var ws = new InMemoryFileWorkspace(@"C:\workspace");
        ws.WriteAllText(@"C:\workspace\src\A.cs", "a");
        ws.WriteAllText(@"C:\workspace\src\B.txt", "b");

        var match = ws.EnumerateFiles(@"C:\workspace\src", "A.cs").ToList();
        await Assert.That(match.Count).IsEqualTo(1);
        await Assert.That(match[0]).EndsWith("A.cs");
    }

    [Test]
    public async Task MoveFile_and_append_and_provider()
    {
        var ws = new InMemoryFileWorkspace(@"C:\ws");
        ws.WriteAllText(@"C:\ws\old.txt", "hello");
        ws.MoveFile(@"C:\ws\old.txt", @"C:\ws\new.txt", overwrite: true);
        await Assert.That(ws.FileExists(@"C:\ws\old.txt")).IsFalse();
        await Assert.That(ws.FileExists(@"C:\ws\new.txt")).IsTrue();

        await ws.AppendAllTextAsync(@"C:\ws\new.txt", " world");
        await Assert.That(await ws.ReadAllTextAsync(@"C:\ws\new.txt")).IsEqualTo("hello world");
        await Assert.That(ws.ReadAllBytes(@"C:\ws\new.txt").Length).IsGreaterThan(0);
    }

    [Test]
    public async Task EnsureDirectoryExists_and_enumerate_entries()
    {
        var ws = new InMemoryFileWorkspace(@"C:\root");
        ws.EnsureDirectoryExists(@"C:\root\sub");
        ws.WriteAllText(@"C:\root\sub\file.txt", "x");

        var entries = ws.EnumerateFileSystemEntries(@"C:\root").ToList();
        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0]).Contains("sub");
    }
}
