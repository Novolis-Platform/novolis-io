using System.Text;
using System.Text.Json;
using Novolis.IO.Ndjson;

namespace Novolis.IO.Unit;

public sealed class NdjsonFileWriterTests
{
    [Test]
    public async Task AppendAsync_WritesUtf8JsonWithLf()
    {
        var path = CreatePath();
        try
        {
            await using (var writer = new AsyncDisposableWriter(new NdjsonFileWriter(path)))
            {
                await writer.Writer.AppendAsync(new { DisplayName = "Å" });
            }

            var bytes = await File.ReadAllBytesAsync(path);
            await Assert.That(bytes[^1]).IsEqualTo((byte)'\n');
            using var document = JsonDocument.Parse(bytes[..^1]);
            await Assert.That(document.RootElement.GetProperty("displayName").GetString()).IsEqualTo("Å");
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task AppendSync_AllowsReaderToOpenAfterEachAppend()
    {
        var path = CreatePath();
        try
        {
            using (var writer = new NdjsonFileWriter(path))
            {
                writer.Append(new { Id = 1 });
                await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
                var record = (await document.ReadAsync()).Records.Single();
                await Assert.That(record.Json!.Value.GetProperty("id").GetInt32()).IsEqualTo(1);

                writer.Append(new { Id = 2 });
                await document.RefreshAsync();
                await Assert.That((await document.ReadAsync()).Records).Count().IsEqualTo(2);
            }
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task Reader_DoesNotExposePartialPhysicalLine()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1", Encoding.UTF8);
            await using var document = await new NdjsonFileReader().OpenAsync(new FileInfo(path));
            await Assert.That((await document.ReadAsync()).Records).IsEmpty();

            await File.AppendAllTextAsync(path, "}\n", Encoding.UTF8);
            await document.RefreshAsync();
            await Assert.That((await document.ReadAsync()).Records).Count().IsEqualTo(1);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task AppendJson_DoesNotReadExistingFile()
    {
        var path = CreatePath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"id\":1}\n", Encoding.UTF8);
            using var writer = new NdjsonFileWriter(path);
            writer.AppendJson(Encoding.UTF8.GetBytes("{\"id\":2}"));

            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("{\"id\":1}\n{\"id\":2}\n");
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task CustomOptions_AreUsedForSerialization()
    {
        var path = CreatePath();
        try
        {
            using var writer = new NdjsonFileWriter(
                path,
                new JsonSerializerOptions { PropertyNamingPolicy = null });
            writer.Append(new { DisplayName = "value" });

            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("{\"DisplayName\":\"value\"}\n");
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task AppendAfterRotation_StartsAtTheCurrentPath()
    {
        var path = CreatePath();
        var rotated = $"{path}.1";
        try
        {
            using var writer = new NdjsonFileWriter(path);
            writer.Append(new { Id = 1 });
            File.Move(path, rotated);
            writer.Append(new { Id = 2 });

            await Assert.That(await File.ReadAllTextAsync(rotated)).Contains("\"id\":1");
            await Assert.That(await File.ReadAllTextAsync(path)).Contains("\"id\":2");
        }
        finally
        {
            Delete(path);
            Delete(rotated);
        }
    }

    [Test]
    public async Task AppendAfterTruncation_WritesToTheNewFileContents()
    {
        var path = CreatePath();
        try
        {
            using var writer = new NdjsonFileWriter(path);
            writer.Append(new { Id = 1 });
            await File.WriteAllTextAsync(path, string.Empty);
            writer.Append(new { Id = 2 });

            await Assert.That(await File.ReadAllTextAsync(path)).DoesNotContain("\"id\":1");
            await Assert.That(await File.ReadAllTextAsync(path)).Contains("\"id\":2");
        }
        finally
        {
            Delete(path);
        }
    }

    private static string CreatePath() =>
        Path.Combine(Path.GetTempPath(), $"novolis-ndjson-writer-{Guid.NewGuid():N}.ndjson");

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

    private sealed class AsyncDisposableWriter(NdjsonFileWriter writer) : IAsyncDisposable
    {
        public NdjsonFileWriter Writer { get; } = writer;

        public ValueTask DisposeAsync()
        {
            Writer.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
