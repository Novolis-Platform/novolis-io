using Novolis.IO.Git;
using Novolis.IO.Processes;
using Novolis.IO.Recovery;
using Novolis.IO.Watching;

namespace Novolis.IO.Unit;

public sealed class IoCoveragePushTests
{
    [Test]
    public async Task ContentRecoveryStore_write_trim_and_clear()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-recovery-");
        try
        {
            var store = new ContentRecoveryStore(temp.FullName, maxSnapshotsPerDocument: 2);
            store.WriteSnapshot("doc/a", "v1");
            store.WriteSnapshot("doc/a", "v2");
            store.WriteSnapshot("doc/a", "v3");

            var latest = store.GetLatest("doc/a");
            await Assert.That(latest).IsNotNull();
            await Assert.That(latest!.Content).IsEqualTo("v3");
            await Assert.That(latest.ContentHash).IsNotNull();
            var mdCount = Directory.GetFiles(temp.FullName, "*.md", SearchOption.AllDirectories).Length;
            await Assert.That(mdCount).IsLessThanOrEqualTo(2);

            store.Clear("doc/a");
            await Assert.That(store.GetLatest("doc/a")).IsNull();
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task ContentRecoveryStore_tolerates_corrupt_meta()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-recovery-corrupt-");
        try
        {
            var store = new ContentRecoveryStore(temp.FullName);
            store.WriteSnapshot("chapter", "body");
            var latest = store.GetLatest("chapter");
            await Assert.That(latest).IsNotNull();
            var meta = Path.ChangeExtension(latest!.RecoveryPath, ".json");
            await File.WriteAllTextAsync(meta, "{bad");
            var reread = store.GetLatest("chapter");
            await Assert.That(reread!.Content).IsEqualTo("body");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task ProcessJobQueue_start_failure_marks_failed()
    {
        var queue = new ProcessJobQueue();
        var job = queue.Enqueue(new ProcessJobSpec
        {
            FileName = OperatingSystem.IsWindows() ? "missing-novolis-io.exe" : "/no/such/binary",
            Arguments = [],
        });
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (DateTime.UtcNow < deadline && job.Status is ProcessJobStatus.Queued or ProcessJobStatus.Running)
            await Task.Delay(50);
        await Assert.That(job.Status).IsEqualTo(ProcessJobStatus.Failed);
        await Assert.That(job.Detail).IsNotNull();
    }

    [Test]
    public async Task ProcessTree_kills_live_process_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            ProcessTree.Kill(999_999);
            return;
        }

        using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c timeout /t 60 /nobreak >nul",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        ProcessTree.Kill(proc.Id);
        await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(proc.HasExited).IsTrue();
    }

    [Test]
    public async Task GitRepositoryService_more_error_paths()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["rev-parse", "--abbrev-ref", "HEAD"], 0, "main\n");
        runner.Set(["status", "--porcelain"], 0, " M dirty.txt\n");
        runner.Set(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], 0, "fatal: no upstream\n");
        runner.Set(["log", "-1", "--format=%cI|%h|%s"], 0, "2026-01-01T00:00:00Z|abc|msg\n");

        var git = new GitRepositoryService(runner);
        var status = git.GetStatus("/repo");
        await Assert.That(status.Dirty).IsTrue();
        await Assert.That(status.DirtyFiles.Count).IsEqualTo(1);
        await Assert.That(status.Upstream).IsNull();
    }

    [Test]
    public async Task GitRepositoryService_checkpoint_add_failure()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["add", "-A"], 1, "", "add failed\n");
        var git = new GitRepositoryService(runner);
        var result = git.Checkpoint("/repo", "msg", new CheckpointOptions { Push = false });
        await Assert.That(result.Ok).IsFalse();
    }

    [Test]
    public async Task GitRepositoryService_checkpoint_push_failure()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        runner.Set(["add", "-A"], 0, "");
        runner.Set(["commit", "-m", "save", "--no-verify"], 0, "[main abc] save\n");
        runner.Set(["rev-parse", "HEAD"], 0, "abc\n");
        runner.Set(["push"], 1, "", "push rejected\n");
        var git = new GitRepositoryService(runner);
        var result = git.Checkpoint("/repo", "save");
        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).Contains("push failed");
    }

    [Test]
    public async Task GitRepositoryService_pass_finish_without_active()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-pass-finish-");
        try
        {
            var runner = new FakeGitRunner();
            runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
            var git = new GitRepositoryService(runner);
            var result = git.PassFinish(temp.FullName);
            await Assert.That(result.Ok).IsFalse();
            await Assert.That(result.Message).Contains("No active pass");
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task GitRepositoryService_create_revision_tag_requires_name()
    {
        var runner = new FakeGitRunner();
        runner.Set(["rev-parse", "--git-dir"], 0, ".git\n");
        var git = new GitRepositoryService(runner);
        var result = git.CreateRevisionTag("/repo", "  ");
        await Assert.That(result.Ok).IsFalse();
    }

    [Test]
    public async Task SingleFileWatcher_stop_and_restart()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-watch-stop-");
        try
        {
            var path = Path.Combine(temp.FullName, "watch.txt");
            await File.WriteAllTextAsync(path, "a");
            using var watcher = new SingleFileWatcher();
            var hits = 0;
            watcher.FileChanged += _ => hits++;
            watcher.Watch(path);
            watcher.Stop();
            await File.WriteAllTextAsync(path, "b");
            await Task.Delay(300);
            await Assert.That(hits).IsEqualTo(0);
            watcher.Watch(path);
            await File.WriteAllTextAsync(path, "c");
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline && hits == 0)
                await Task.Delay(50);
            await Assert.That(hits).IsGreaterThan(0);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task ProcessGitRunner_runs_git_in_temp_repo()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var temp = Directory.CreateTempSubdirectory("novolis-io-git-runner-");
        try
        {
            RunGit(temp.FullName, "init");
            RunGit(temp.FullName, "config", "user.email", "test@test.com");
            RunGit(temp.FullName, "config", "user.name", "Test");
            await File.WriteAllTextAsync(Path.Combine(temp.FullName, "a.txt"), "x");
            RunGit(temp.FullName, "add", "a.txt");
            RunGit(temp.FullName, "commit", "-m", "init");

            var runner = new ProcessGitRunner();
            var status = runner.Run(temp.FullName, "status", "--porcelain");
            await Assert.That(status.ExitCode).IsEqualTo(0);
            await Assert.That(status.StdOut.Trim()).IsEqualTo("");
        }
        finally
        {
            try { temp.Delete(true); } catch { /* git may lock .git on Windows */ }
        }
    }

    static void RunGit(string dir, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        using var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException(p.StandardError.ReadToEnd());
    }
}
