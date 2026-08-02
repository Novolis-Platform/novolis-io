using Novolis.IO.Paths;
using Novolis.IO.Recovery;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class RootFinderTests
{
    [Test]
    public async Task TryFind_returns_false_when_no_markers()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-root-");
        try
        {
            var nested = Path.Combine(temp.FullName, "deep", "nested");
            Directory.CreateDirectory(nested);
            var ok = RootFinder.TryFind(nested, ["missing.txt"], out var root);
            await Assert.That(ok).IsFalse();
            await Assert.That(root).IsEqualTo(nested);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task TryFind_predicate_matches_custom_root()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-root-predicate-");
        try
        {
            File.WriteAllText(Path.Combine(temp.FullName, "ROOT"), "x");
            var nested = Path.Combine(temp.FullName, "child");
            Directory.CreateDirectory(nested);
            var ok = RootFinder.TryFind(nested, dir => File.Exists(Path.Combine(dir.FullName, "ROOT")), out var root);
            await Assert.That(ok).IsTrue();
            await Assert.That(root).IsEqualTo(temp.FullName);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task TryFind_rejects_empty_marker_list()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-root-empty-");
        try
        {
            var ok = RootFinder.TryFind(temp.FullName, [], out _);
            await Assert.That(ok).IsFalse();
        }
        finally
        {
            temp.Delete(true);
        }
    }
}

public sealed class RecoveryExtendedTests
{
    [Test]
    public async Task WriteSnapshot_trims_to_max_per_document()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-recovery-trim-");
        try
        {
            var store = new ContentRecoveryStore(temp.FullName, maxSnapshotsPerDocument: 2);
            store.WriteSnapshot("doc", "v1");
            Thread.Sleep(1100);
            store.WriteSnapshot("doc", "v2");
            Thread.Sleep(1100);
            store.WriteSnapshot("doc", "v3");
            var latest = store.GetLatest("doc");
            await Assert.That(latest).IsNotNull();
            await Assert.That(latest!.Content).IsEqualTo("v3");
            var dir = Directory.GetDirectories(temp.FullName);
            await Assert.That(dir.Length).IsEqualTo(1);
            var mdFiles = Directory.GetFiles(dir[0], "*.md");
            await Assert.That(mdFiles.Length).IsLessThanOrEqualTo(2);
            await Assert.That(mdFiles.Length).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task WriteSnapshot_isolates_documents_by_key()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-recovery-keys-");
        try
        {
            var store = new ContentRecoveryStore(temp.FullName);
            store.WriteSnapshot("alpha", "a");
            store.WriteSnapshot("beta", "b");
            await Assert.That(store.GetLatest("alpha")!.Content).IsEqualTo("a");
            await Assert.That(store.GetLatest("beta")!.Content).IsEqualTo("b");
        }
        finally
        {
            temp.Delete(true);
        }
    }
}

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
