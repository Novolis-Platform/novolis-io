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
