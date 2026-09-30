using Novolis.IO.GitHub;
using Novolis.IO.Processes;
using Novolis.IO.Watching;

namespace Novolis.IO.Unit;

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
