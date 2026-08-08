using Novolis.IO.Git;

namespace Novolis.IO.Unit;

public sealed class GitRepositoryServiceTests
{
    [Test]
    public async Task GetStatus_parses_ahead_behind_and_pass_store()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-git-");
        try
        {
            var passDir = Path.Combine(temp.FullName, ".novolis");
            Directory.CreateDirectory(passDir);
            await File.WriteAllTextAsync(
                Path.Combine(passDir, "git-passes.json"),
                """{"activePass":"20260101T000000Z","passes":[{"id":"20260101T000000Z","branch":"pass/20260101T000000Z","startedAt":"2026-01-01T00:00:00Z"}]}""");

            var runner = new FakeGitRunner();
            runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
            runner.Set(["status", "-b", "--porcelain"], 0, "## feature...origin/feature [ahead 3, behind 2]\n");
            runner.Set(["log", "-1", "--format=%cI|%h|%s"], 0, "2026-01-02T00:00:00Z|deadbeef|msg\n");

            var git = new GitRepositoryService(runner);
            var status = git.GetStatus(temp.FullName);
            await Assert.That(status.Branch).IsEqualTo("feature");
            await Assert.That(status.Upstream).IsEqualTo("origin/feature");
            await Assert.That(status.Ahead).IsEqualTo(3);
            await Assert.That(status.Behind).IsEqualTo(2);
            await Assert.That(status.Dirty).IsFalse();
            await Assert.That(status.ActivePass).IsEqualTo("20260101T000000Z");
            await Assert.That(status.LastCommitSha).IsEqualTo("deadbeef");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Checkpoint_stages_pathspecs_and_commits()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["add", "--", "src/a.cs"], 0, "");
        runner.Set(["commit", "-m", "save", "--no-verify"], 0, "[main abc1234] save\n");
        runner.Set(["rev-parse", "HEAD"], 0, "abc1234\n");
        runner.Set(["push"], 0, "");

        var git = new GitRepositoryService(runner);
        var result = git.Checkpoint("/repo", "save", new CheckpointOptions
        {
            Pathspecs = ["src/a.cs"],
            Push = true,
        });
        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Message).Contains("Checkpoint committed");
    }

    [Test]
    public async Task Checkpoint_nothing_to_commit_fails()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["add", "-A"], 0, "");
        runner.Set(["commit", "-m", "save", "--no-verify"], 1, "", "nothing to commit, working tree clean\n");

        var git = new GitRepositoryService(runner);
        var result = git.Checkpoint("/repo", "save", new CheckpointOptions { Push = false });
        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo("Nothing to commit.");
    }

    [Test]
    public async Task Checkpoint_requires_message()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        var git = new GitRepositoryService(runner);
        var result = git.Checkpoint("/repo", "  ");
        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo("Message is required.");
    }

    [Test]
    public async Task PassStart_and_finish_lifecycle()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-pass-");
        try
        {
            var runner = new FakeGitRunner();
            runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
            runner.Set(["checkout", "-b"], 0, ""); // wildcard not supported - need exact match

            // PassStart uses dynamic branch name - use flexible runner
            var flex = new FlexibleGitRunner();
            flex.When(args => args.Length >= 2 && args[0] == "rev-parse" && args[1] == "--git-dir", 0, ".git\n");
            flex.When(args => args.Length >= 2 && args[0] == "checkout" && args[1] == "-b", 0, "");
            flex.When(args => args.Length >= 2 && args[0] == "push" && args[1] == "-u", 0, "");
            flex.When(args => args.Length >= 2 && args[0] == "checkout" && args[1] == "main", 0, "");
            flex.When(args => args.Length >= 2 && args[0] == "merge", 0, "");
            flex.When(args => args is ["push", "origin", "main"], 0, "");
            flex.When(args => args.Length >= 2 && args[0] == "rev-parse" && args[1] == "--verify", 0, "");

            var git = new GitRepositoryService(flex);
            var start = git.PassStart(temp.FullName);
            await Assert.That(start.Ok).IsTrue();

            var finish = git.PassFinish(temp.FullName);
            await Assert.That(finish.Ok).IsTrue();
            await Assert.That(finish.Message).Contains("Merged pass");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task PassStart_rejects_when_active()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-pass-active-");
        try
        {
            var passDir = Path.Combine(temp.FullName, ".novolis");
            Directory.CreateDirectory(passDir);
            await File.WriteAllTextAsync(
                Path.Combine(passDir, "git-passes.json"),
                """{"activePass":"existing","passes":[]}""");

            var runner = new FakeGitRunner();
            runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
            var git = new GitRepositoryService(runner);
            var result = git.PassStart(temp.FullName);
            await Assert.That(result.Ok).IsFalse();
            await Assert.That(result.Message).Contains("Pass already active");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task CreateRevisionTag_checks_out_default_branch()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["rev-parse", "--verify", "main"], 0, "abc\n");
        runner.Set(["checkout", "main"], 0, "");
        runner.Set(["tag", "v1.0.0", "main"], 0, "");
        runner.Set(["push", "origin", "v1.0.0"], 0, "");

        var git = new GitRepositoryService(runner);
        var result = git.CreateRevisionTag("/repo", "v1.0.0");
        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Message).Contains("Tagged v1.0.0");
    }

    [Test]
    public async Task Not_a_git_repo_throws()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 128, "", "fatal: not a git repository\n");
        var git = new GitRepositoryService(runner);
        await Assert.That(() => git.GetStatus("/nope")).Throws<InvalidOperationException>();
    }
}

sealed class FlexibleGitRunner : IGitProcessRunner
{
    readonly List<(Func<string[], bool> match, GitProcessResult result)> _rules = [];

    public void When(Func<string[], bool> match, int exitCode, string stdout, string stderr = "") =>
        _rules.Add((match, new GitProcessResult(exitCode, stdout, stderr)));

    public GitProcessResult Run(string workingDirectory, params string[] args)
    {
        foreach (var (match, result) in _rules)
        {
            if (match(args))
                return result;
        }

        return new GitProcessResult(1, "", "unexpected: " + string.Join(' ', args));
    }
}
