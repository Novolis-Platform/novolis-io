using Novolis.IO.Git;

namespace Novolis.IO.Unit;

public sealed class GitRepositoryServiceExtendedTests
{
    [Test]
    public async Task PassStart_push_failure()
    {
        var flex = new FlexibleGitRunner();
        flex.When(args => args is ["rev-parse", "--git-dir"], 0, ".git\n");
        flex.When(args => args.Length >= 2 && args[0] == "checkout" && args[1] == "-b", 0, "");
        flex.When(args => args.Length >= 2 && args[0] == "push" && args[1] == "-u", 1, "", "remote rejected\n");
        var git = new GitRepositoryService(flex);
        var result = git.PassStart("/repo");
        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).Contains("remote rejected");
    }

    [Test]
    public async Task PassFinish_merge_failure()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-pass-merge-fail-");
        try
        {
            var passDir = Path.Combine(temp.FullName, ".novolis");
            Directory.CreateDirectory(passDir);
            await File.WriteAllTextAsync(
                Path.Combine(passDir, "git-passes.json"),
                """{"activePass":"20260101T000000Z","passes":[{"id":"20260101T000000Z","branch":"pass/20260101T000000Z","startedAt":"2026-01-01T00:00:00Z"}]}""");

            var flex = new FlexibleGitRunner();
            flex.When(args => args is ["rev-parse", "--git-dir"], 0, ".git\n");
            flex.When(args => args.Length >= 2 && args[0] == "rev-parse" && args[1] == "--verify", 0, "");
            flex.When(args => args is ["checkout", "main"], 0, "");
            flex.When(args => args.Length >= 2 && args[0] == "merge", 1, "", "conflict\n");

            var git = new GitRepositoryService(flex);
            var result = git.PassFinish(temp.FullName);
            await Assert.That(result.Ok).IsFalse();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task CreateRevisionTag_tag_failure()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["rev-parse", "--verify", "main"], 0, "abc\n");
        runner.Set(["checkout", "main"], 0, "");
        runner.Set(["tag", "v2", "main"], 1, "", "tag exists\n");
        var git = new GitRepositoryService(runner);
        var result = git.CreateRevisionTag("/repo", "v2");
        await Assert.That(result.Ok).IsFalse();
    }

    [Test]
    public async Task Checkpoint_commit_generic_failure()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["add", "-A"], 0, "");
        runner.Set(["commit", "-m", "save", "--no-verify"], 1, "", "hook failed\n");
        var git = new GitRepositoryService(runner);
        var result = git.Checkpoint("/repo", "save", new CheckpointOptions { Push = false });
        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).Contains("hook failed");
    }

    [Test]
    public async Task DetectDefaultBranch_uses_symbolic_ref()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["rev-parse", "--verify", "main"], 128, "", "fatal\n");
        runner.Set(["rev-parse", "--verify", "master"], 128, "", "fatal\n");
        runner.Set(["symbolic-ref", "refs/remotes/origin/HEAD"], 0, "refs/remotes/origin/develop\n");
        runner.Set(["status", "--porcelain"], 0, "");
        runner.Set(["rev-parse", "--abbrev-ref", "HEAD"], 0, "develop\n");
        runner.Set(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], 0, "fatal\n");
        runner.Set(["log", "-1", "--format=%cI|%h|%s"], 0, "2026-01-01T00:00:00Z|abc|msg\n");
        var git = new GitRepositoryService(runner);
        var status = git.GetStatus("/repo");
        await Assert.That(status.Branch).IsEqualTo("develop");
    }
}
