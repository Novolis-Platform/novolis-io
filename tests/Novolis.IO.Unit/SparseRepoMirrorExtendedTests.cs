using Novolis.IO.GitHub;
using Novolis.IO.Processes;
using Novolis.IO.Watching;

namespace Novolis.IO.Unit;

public sealed class SparseRepoMirrorExtendedTests
{
    [Test]
    public async Task NoteDirty_deduplicates_and_persists()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-dirty-");
        try
        {
            var mirror = new SparseRepoMirror(
                SparseRepoMirror.CreateClient("gho_test"),
                new SparseRepoMirrorOptions { Owner = "o", Name = "n", WorkspaceRoot = temp.FullName });

            mirror.NoteDirty("content/a.md");
            mirror.NoteDirty("content/a.md");
            mirror.NoteDirty("content/b.md");
            await Assert.That(mirror.DirtyCount).IsEqualTo(2);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task SaveCommitPush_without_pull_returns_error()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-nopull-");
        try
        {
            var mirror = new SparseRepoMirror(
                SparseRepoMirror.CreateClient("gho_test"),
                new SparseRepoMirrorOptions { Owner = "o", Name = "n", WorkspaceRoot = temp.FullName });
            mirror.NoteDirty("content/x.md");
            var result = await mirror.SaveCommitPushAsync();
            await Assert.That(result.Ok).IsFalse();
            await Assert.That(result.Message).Contains("Pull before");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Branch_reads_state_and_corrupt_json_resets()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-mirror-state-");
        try
        {
            var mirror = new SparseRepoMirror(
                SparseRepoMirror.CreateClient("gho_test"),
                new SparseRepoMirrorOptions { Owner = "o", Name = "n", WorkspaceRoot = temp.FullName });
            await Assert.That(mirror.Branch).IsNull();

            var stateDir = Path.Combine(temp.FullName, ".novolis");
            Directory.CreateDirectory(stateDir);
            await File.WriteAllTextAsync(
                Path.Combine(stateDir, "mobile-mirror-o-n.json"),
                """{"branch":"main","commitSha":"abc","files":{},"dirty":[]}""");
            await Assert.That(mirror.Branch).IsEqualTo("main");

            await File.WriteAllTextAsync(Path.Combine(stateDir, "mobile-mirror-o-n.json"), "{not-json");
            await Assert.That(mirror.DirtyCount).IsEqualTo(0);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task CreateClient_rejects_blank_token()
    {
        await Assert.That(() => SparseRepoMirror.CreateClient("")).Throws<ArgumentException>();
    }
}
