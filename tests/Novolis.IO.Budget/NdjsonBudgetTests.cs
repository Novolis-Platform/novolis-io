using System.Text;
using Novolis.IO.Ndjson;
using Novolis.Testing.Budgets;
using BudgetLimits = Novolis.Testing.Budgets.Budget;

namespace Novolis.IO.Budget;

[NotInParallel("ndjson-budget")]
[Category("Budget")]
public sealed class NdjsonBudgetTests
{
    private const int SizeBytes = 4 * 1024 * 1024;

    // Wide gate until a hosted log prints the highlight row. Refresh measures append of 1,000
    // records plus one Refresh; truncate and the baseline refresh run in Prepare and are not timed.
    private static readonly BudgetLimits FirstSliceCeiling = new(
        MaxElapsed: TimeSpan.FromSeconds(1),
        MaxAllocatedBytes: 16L * 1024 * 1024);

    private static readonly BudgetLimits IndexCeiling = new(
        MaxElapsed: TimeSpan.FromSeconds(5),
        MaxAllocatedBytes: 32L * 1024 * 1024);

    private static readonly BudgetLimits SeekCeiling = new(
        MaxElapsed: TimeSpan.FromSeconds(1),
        MaxAllocatedBytes: 16L * 1024 * 1024,
        MinThroughput: 5);

    private static readonly BudgetLimits RefreshCeiling = new(
        MaxElapsed: TimeSpan.FromSeconds(5),
        MaxAllocatedBytes: 32L * 1024 * 1024);

    [Test]
    public async Task TimeToFirstSlice_stays_within_ceiling()
    {
        var file = CreateShortFile();
        try
        {
            var observed = 0;
            var sample = await BudgetProbe.MeasureAsync(Run("TimeToFirstSlice", "slices/s"), async cancellationToken =>
            {
                await using var document = await new NdjsonFileReader().OpenAsync(file, cancellationToken);
                var slice = await document.ReadAsync(take: 100, cancellationToken: cancellationToken);
                observed = slice.Records.Count;
            });
            await Assert.That(observed).IsEqualTo(100);
            await sample.AssertWithin(FirstSliceCeiling);
        }
        finally
        {
            Delete(file);
        }
    }

    [Test]
    public async Task IndexFullFile_stays_within_ceiling()
    {
        var file = CreateShortFile();
        try
        {
            var observed = 0;
            var sample = await BudgetProbe.MeasureAsync(Run("IndexFullFile", "indexes/s"), async cancellationToken =>
            {
                await using var document = await new NdjsonFileReader().OpenAsync(file, cancellationToken);
                await document.RefreshAsync(cancellationToken);
                var slice = await document.ReadAsync(take: 100, cancellationToken: cancellationToken);
                observed = slice.Records.Count;
            });
            await Assert.That(observed).IsEqualTo(100);
            await sample.AssertWithin(IndexCeiling);
        }
        finally
        {
            Delete(file);
        }
    }

    [Test]
    public async Task RandomSliceSeek_stays_within_ceiling()
    {
        var file = CreateShortFile();
        try
        {
            await using var indexed = await new NdjsonFileReader(new NdjsonOpenOptions(IndexInterval: 10_000, MaxTake: 1_000))
                .OpenAsync(file);
            await indexed.RefreshAsync();
            var skip = Math.Max(0, indexed.RecordCount / 2);
            var observed = 0;
            var sample = await BudgetProbe.MeasureAsync(Run("RandomSliceSeek", "seeks/s"), async cancellationToken =>
            {
                var slice = await indexed.ReadAsync(skip, 100, cancellationToken);
                observed = slice.Records.Count;
            });
            await Assert.That(observed).IsEqualTo(100);
            await sample.AssertWithin(SeekCeiling);
        }
        finally
        {
            Delete(file);
        }
    }

    [Test]
    public async Task IncrementalRefresh_stays_within_ceiling()
    {
        var file = CreateShortFile();
        try
        {
            var baseline = file.Length;
            await using var document = await new NdjsonFileReader().OpenAsync(file);
            await document.RefreshAsync();
            var startCount = document.RecordCount;
            var observed = 0L;
            var sample = await BudgetProbe.MeasureAsync(
                Run("IncrementalRefresh", "refreshes/s") with
                {
                    Prepare = async cancellationToken =>
                    {
                        Truncate(file, baseline);
                        await document.RefreshAsync(cancellationToken);
                    },
                },
                async cancellationToken =>
                {
                    Append(file, 1_000, startCount);
                    await document.RefreshAsync(cancellationToken);
                    observed = document.RecordCount;
                });
            await Assert.That(observed).IsEqualTo(startCount + 1_000);
            await sample.AssertWithin(RefreshCeiling);
        }
        finally
        {
            Delete(file);
        }
    }

    [Test]
    [Category("BenchmarkReport")]
    public async Task Profile_report_prints_when_present()
    {
        var artifacts = FindArtifactsDirectory();
        var report = artifacts is null ? null : BenchmarkReport.FindLatest(artifacts);
        Skip.Unless(report is not null, "Run benchmarks/Novolis.IO.Ndjson.Benchmarks locally to produce a JSON report.");
        var read = BenchmarkReport.TryRead(report, out var rows);
        Highlight.Write(rows);
        await Assert.That(read).IsTrue();
        await Assert.That(rows.Count).IsGreaterThan(0);
    }

    private static BudgetRun Run(string probe, string unit) =>
        new(
            probe,
            Iterations: 5,
            Warmup: 1,
            OperationsPerIteration: 1,
            Unit: unit,
            Parameters: "4 MiB short/LF",
            Allocations: AllocationScope.Process);

    private static FileInfo CreateShortFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"novolis-ndjson-budget-{Guid.NewGuid():N}.ndjson");
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        var record = 0L;
        while (stream.Length < SizeBytes)
        {
            var bytes = Encoding.UTF8.GetBytes($"{{\"id\":{record},\"payload\":\"short\"}}\n");
            stream.Write(bytes);
            record++;
        }

        return new FileInfo(path);
    }

    private static void Append(FileInfo file, int count, long startingRecord)
    {
        using var stream = new FileStream(file.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        for (var index = 0; index < count; index++)
        {
            var bytes = Encoding.UTF8.GetBytes($"{{\"id\":{startingRecord + index},\"payload\":\"short\"}}\n");
            stream.Write(bytes);
        }
    }

    private static void Truncate(FileInfo file, long length)
    {
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        stream.SetLength(length);
    }

    private static void Delete(FileInfo file)
    {
        file.Refresh();
        if (file.Exists)
            file.Delete();
    }

    private static string? FindArtifactsDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "Novolis.IO.slnx")))
                continue;

            var candidate = Path.Combine(dir.FullName, "benchmarks", "Novolis.IO.Ndjson.Benchmarks", "BenchmarkDotNet.Artifacts");
            return Directory.Exists(candidate) ? candidate : null;
        }

        return null;
    }
}
