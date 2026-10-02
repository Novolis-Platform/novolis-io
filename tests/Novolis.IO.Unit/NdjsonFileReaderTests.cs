using System.Text;
using Novolis.IO.Ndjson;

namespace Novolis.IO.Unit;

public sealed class NdjsonFileReaderTests
{
    [Test]
    public async Task EmptyFile_HasNoRecords()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllBytesAsync(path, []);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));

            var slice = await document.ReadAsync();

            await Assert.That(slice.Records).IsEmpty();
            await Assert.That(slice.HasMore).IsFalse();
            await Assert.That(document.RecordCount).IsEqualTo(0);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task SingleRecord_RequiresTerminalNewline()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));

            var slice = await document.ReadAsync();

            await Assert.That(slice.Records).IsEmpty();
            await Assert.That(document.RecordCount).IsEqualTo(0);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task SingleRecord_WithTerminalNewline_IsReadable()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));

            var record = (await document.ReadAsync()).Records.Single();

            await Assert.That(record.Number).IsEqualTo(0);
            await Assert.That(record.ByteOffset).IsEqualTo(0);
            await Assert.That(record.IsValid).IsTrue();
            await Assert.That(record.Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(1);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task CrLf_OffsetsBeginAfterCompleteNewlines()
    {
        var path = CreatePath();
        try
        {
            var content = "{\"id\":1}\r\n{\"id\":2}\r\n";
            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));

            var slice = await document.ReadAsync(take: 10);

            await Assert.That(slice.Records.Select(static record => record.Number)).IsEquivalentTo([0L, 1L]);
            await Assert.That(slice.Records[1].ByteOffset)
                .IsEqualTo(Encoding.UTF8.GetPreamble().Length + Encoding.UTF8.GetByteCount("{\"id\":1}\r\n"));
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task MalformedRecord_DoesNotStopFollowingRecords()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\nnot-json\n{\"id\":3}\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));

            var records = (await document.ReadAsync(take: 10)).Records;

            await Assert.That(records).Count().IsEqualTo(3);
            await Assert.That(records[0].IsValid).IsTrue();
            await Assert.That(records[1].IsValid).IsFalse();
            await Assert.That(records[1].Raw).IsEqualTo("not-json");
            await Assert.That(records[1].Error).IsNotNull();
            await Assert.That(records[2].Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(3);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task Utf8Bom_IsToleratedAtTheBeginning()
    {
        var path = CreatePath();
        try
        {
            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes("{\"id\":7}\n"))
                .ToArray();
            await File.WriteAllBytesAsync(path, bytes);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));

            var record = (await document.ReadAsync()).Records.Single();

            await Assert.That(record.Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(7);
            await Assert.That(record.ByteOffset).IsEqualTo(0);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task RandomSeeking_ReturnsExactRecordNumbers()
    {
        var path = CreatePath();
        try
        {
            var content = string.Join(
                string.Empty,
                Enumerable.Range(0, 250).Select(static id => $"{{\"id\":{id}}}\n"));
            await File.WriteAllTextAsync(path, content, Encoding.UTF8);
            await using var document = await new NdjsonFileReader(
                new NdjsonOpenOptions(IndexInterval: 10, MaxTake: 100)).OpenAsync(new FileInfo(path));

            var slice = await document.ReadAsync(skip: 137, take: 7);

            await Assert.That(slice.Records.Select(static record => record.Number))
                .IsEquivalentTo(Enumerable.Range(137, 7).Select(static value => (long)value));
            await Assert.That(slice.HasPrevious).IsTrue();
            await Assert.That(slice.HasMore).IsTrue();
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task PartialRecord_BecomesVisibleAfterAppendCompletion()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
            await Assert.That((await document.ReadAsync()).Records).IsEmpty();

            await File.AppendAllTextAsync(path, "}\n", Encoding.UTF8);
            await document.RefreshAsync();
            var record = (await document.ReadAsync()).Records.Single();

            await Assert.That(record.Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(1);
            await Assert.That(document.RecordCount).IsEqualTo(1);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task AppendMultipleRecords_AreAvailableAfterRefresh()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
            await document.ReadAsync();

            await File.AppendAllTextAsync(path, "{\"id\":2}\n{\"id\":3}\n", Encoding.UTF8);
            await document.RefreshAsync();
            var slice = await document.ReadAsync(take: 10);

            await Assert.That(slice.Records).Count().IsEqualTo(3);
            await Assert.That(document.RecordCount).IsEqualTo(3);
            await Assert.That(slice.Records[2].Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(3);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task AppendRefresh_ScansOnlyTheAppendedBytes()
    {
        var path = CreatePath();
        try
        {
            const string initial = "{\"id\":1}\n";
            const string appended = "{\"id\":2}\n{\"id\":3}\n";
            await File.WriteAllTextAsync(path, initial, Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
            await document.ReadAsync();

            await File.AppendAllTextAsync(path, appended, Encoding.UTF8);
            await document.RefreshAsync();

            var concrete = (Novolis.IO.Ndjson.NdjsonDocument)document;
            await Assert.That(concrete.LastScanBytes)
                .IsEqualTo(Encoding.UTF8.GetByteCount(appended));
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task Truncation_RebuildsTheIndex()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\n{\"id\":2}\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
            await document.ReadAsync();

            await File.WriteAllTextAsync(path, "{\"id\":9}\n", Encoding.UTF8);
            await document.RefreshAsync();
            var records = (await document.ReadAsync()).Records;

            await Assert.That(records).Count().IsEqualTo(1);
            await Assert.That(records[0].Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(9);
            await Assert.That(document.RecordCount).IsEqualTo(1);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task ReplacementWithSameLength_InvalidatesTheIndex()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
            await document.ReadAsync();

            await File.WriteAllTextAsync(path, "{\"id\":2}\n", Encoding.UTF8);
            await document.RefreshAsync();
            var record = (await document.ReadAsync()).Records.Single();

            await Assert.That(record.Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(2);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task SliceValidation_RejectsInvalidBounds()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader(
                new NdjsonOpenOptions(MaxTake: 5)).OpenAsync(new FileInfo(path));

            await Assert.That(async () => await document.ReadAsync(skip: -1)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(async () => await document.ReadAsync(take: 0)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(async () => await document.ReadAsync(take: 6)).Throws<ArgumentOutOfRangeException>();
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task Cancellation_IsHonoredBeforeIndexing()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.That(async () => await document.ReadAsync(cancellationToken: cancellation.Token))
                .Throws<OperationCanceledException>();
            await Assert.That(async () => await document.RefreshAsync(cancellation.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task VeryLargeRecord_IsReportedWithoutUnboundedParsing()
    {
        var path = CreatePath();
        try
        {
            var content = new string('x', (32 * 1024 * 1024) + 1);
            await File.WriteAllTextAsync(path, $"\"{content}\"\n", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));

            var record = (await document.ReadAsync()).Records.Single();

            await Assert.That(record.IsValid).IsFalse();
            await Assert.That(record.Raw).Contains("exceeds");
        }
        finally
        {
            Delete(path);
        }
    }

    private static string CreatePath() =>
        Path.Combine(Path.GetTempPath(), $"novolis-ndjson-{Guid.NewGuid():N}.ndjson");

    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
