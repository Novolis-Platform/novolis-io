using Novolis.IO.Git;
using Novolis.IO.Paths;
using Novolis.IO.Recovery;

namespace Novolis.IO.Unit;

public sealed class IoTests
{
    [Test]
    public async Task RootFinder_Finds_Markers()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-paths-");
        try
        {
            var nested = Path.Combine(temp.FullName, "a", "b");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(temp.FullName, "marker.txt"), "x");
            Directory.CreateDirectory(Path.Combine(temp.FullName, "content"));

            var ok = RootFinder.TryFind(nested, ["marker.txt", "content"], out var root);
            await Assert.That(ok).IsTrue();
            await Assert.That(root).IsEqualTo(temp.FullName);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Recovery_RoundTrip()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-recovery-");
        try
        {
            var store = new ContentRecoveryStore(temp.FullName, maxSnapshotsPerDocument: 3);
            store.WriteSnapshot("chapter-1", "hello");
            var latest = store.GetLatest("chapter-1");
            await Assert.That(latest).IsNotNull();
            await Assert.That(latest!.Content).IsEqualTo("hello");
            await Assert.That(latest.ContentHash).IsNotNull();
            store.Clear("chapter-1");
            await Assert.That(store.GetLatest("chapter-1")).IsNull();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Git_GetStatus_Uses_Runner()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["rev-parse", "--abbrev-ref", "HEAD"], 0, "main\n");
        runner.Set(["status", "--porcelain"], 0, " M file.txt\n");
        runner.Set(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], 128, "", "fatal: no upstream\n");
        runner.Set(["log", "-1", "--format=%cI|%h|%s"], 0, "2026-01-01T00:00:00Z|abc123|hello\n");

        var git = new GitRepositoryService(runner);
        var status = git.GetStatus(Path.GetTempPath());
        await Assert.That(status.Branch).IsEqualTo("main");
        await Assert.That(status.Dirty).IsTrue();
        await Assert.That(status.LastCommitSha).IsEqualTo("abc123");
    }
}

sealed class FakeGitRunner : IGitProcessRunner
{
    readonly Dictionary<string, GitProcessResult> _map = new(StringComparer.Ordinal);

    public void Set(string[] args, int exitCode, string stdout, string stderr = "") =>
        _map[string.Join('\0', args)] = new GitProcessResult(exitCode, stdout, stderr);

    public GitProcessResult Run(string workingDirectory, params string[] args)
    {
        var key = string.Join('\0', args);
        if (_map.TryGetValue(key, out var result))
            return result;
        return new GitProcessResult(1, "", "unexpected: " + string.Join(' ', args));
    }
}
