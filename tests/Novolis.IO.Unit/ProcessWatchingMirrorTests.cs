using Novolis.IO.GitHub;
using Novolis.IO.Processes;
using Novolis.IO.Watching;

namespace Novolis.IO.Unit;

public sealed class ProcessJobQueueTests
{
    static ProcessJobSpec EchoSpec(int exitCode) =>
        OperatingSystem.IsWindows()
            ? new ProcessJobSpec { FileName = "cmd.exe", Arguments = ["/c", $"exit /b {exitCode}"], Title = "echo" }
            : new ProcessJobSpec { FileName = "/bin/sh", Arguments = ["-c", $"exit {exitCode}"], Title = "echo" };

    static async Task WaitForStatus(ProcessJob job, ProcessJobStatus status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (job.Status == status)
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException($"Job {job.Id} stayed {job.Status}; expected {status}. Detail: {job.Detail}");
    }

    [Test]
    public async Task Enqueue_runs_short_lived_process_to_success()
    {
        var queue = new ProcessJobQueue { MaxParallel = 1 };
        var job = queue.Enqueue(EchoSpec(0));
        await WaitForStatus(job, ProcessJobStatus.Succeeded, TimeSpan.FromSeconds(10));
        await Assert.That(job.ExitCode).IsEqualTo(0);
        await Assert.That(job.Detail).Contains("Succeeded");
    }

    [Test]
    public async Task Enqueue_marks_failure_for_nonzero_exit()
    {
        var queue = new ProcessJobQueue();
        var job = queue.Enqueue(EchoSpec(7));
        await WaitForStatus(job, ProcessJobStatus.Failed, TimeSpan.FromSeconds(10));
        await Assert.That(job.ExitCode).IsEqualTo(7);
        await Assert.That(job.Detail).Contains("Exit code 7");
    }

    [Test]
    public async Task Cancel_queued_job_before_start()
    {
        var queue = new ProcessJobQueue { MaxParallel = 1 };
        var blocker = queue.Enqueue(new ProcessJobSpec
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? ["/c", "timeout /t 5 /nobreak >nul"] : ["-c", "sleep 5"],
            Title = "blocker",
        });
        await WaitForStatus(blocker, ProcessJobStatus.Running, TimeSpan.FromSeconds(5));

        var pending = queue.Enqueue(EchoSpec(0));
        queue.Cancel(pending);
        await Assert.That(pending.Status).IsEqualTo(ProcessJobStatus.Cancelled);
        await Assert.That(pending.Detail).Contains("Cancelled before start");
    }

    [Test]
    public async Task MaxParallel_limits_concurrency()
    {
        var queue = new ProcessJobQueue { MaxParallel = 1 };
        var first = queue.Enqueue(new ProcessJobSpec
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? ["/c", "timeout /t 2 /nobreak >nul"] : ["-c", "sleep 2"],
        });
        var second = queue.Enqueue(EchoSpec(0));
        await WaitForStatus(first, ProcessJobStatus.Running, TimeSpan.FromSeconds(5));
        await Assert.That(second.Status).IsEqualTo(ProcessJobStatus.Queued);
        await WaitForStatus(second, ProcessJobStatus.Succeeded, TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Enqueue_null_spec_throws()
    {
        var queue = new ProcessJobQueue();
        await Assert.That(() => queue.Enqueue(null!)).Throws<ArgumentNullException>();
    }
}

public sealed class SingleFileWatcherTests
{
    static async Task WaitForEvent(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("File change event was not observed.");
    }

    [Test]
    public async Task Watch_detects_content_change()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-watch-");
        try
        {
            var path = Path.Combine(temp.FullName, "target.txt");
            await File.WriteAllTextAsync(path, "a");

            using var watcher = new SingleFileWatcher();
            string? changed = null;
            watcher.FileChanged += p => changed = p;
            watcher.Watch(path);

            await File.WriteAllTextAsync(path, "b");
            await WaitForEvent(() => changed == path, TimeSpan.FromSeconds(5));
            await Assert.That(changed).IsEqualTo(path);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Watch_ignores_missing_and_empty_paths()
    {
        using var watcher = new SingleFileWatcher();
        watcher.Watch("");
        watcher.Watch(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt"));
        watcher.Stop();
        watcher.Dispose();
    }

    [Test]
    public async Task Debounced_watcher_coalesces_rapid_changes()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-debounce-");
        try
        {
            var path = Path.Combine(temp.FullName, "debounce.txt");
            await File.WriteAllTextAsync(path, "start");

            using var watcher = new DebouncedFileWatcher(debounceMilliseconds: 200);
            var hits = 0;
            watcher.FileChanged += _ => hits++;
            watcher.Watch(path);

            for (var i = 0; i < 5; i++)
                await File.WriteAllTextAsync(path, i.ToString());

            await Task.Delay(500);
            await Assert.That(hits).IsEqualTo(1);
        }
        finally
        {
            temp.Delete(true);
        }
    }
}

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
                Path.Combine(stateDir, "mobile-mirror.json"),
                """{"branch":"main","commitSha":"abc","files":{},"dirty":[]}""");
            await Assert.That(mirror.Branch).IsEqualTo("main");

            await File.WriteAllTextAsync(Path.Combine(stateDir, "mobile-mirror.json"), "{not-json");
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
