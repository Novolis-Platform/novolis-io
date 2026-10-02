using System.Text;
using BenchmarkDotNet.Attributes;
using Novolis.IO.Ndjson;

namespace Novolis.IO.Ndjson.Benchmarks;

[MemoryDiagnoser]
public sealed class NdjsonBenchmarks
{
    private const int IndexInterval = 10_000;
    private readonly Random _random = new(1729);
    private FileInfo? _file;
    private INdjsonDocument? _indexedDocument;
    private long _recordCount;
    private long _baselineLength;
    private long _baselineRecordCount;

    [Params(100)]
    public int SizeMegabytes { get; set; }

    [Params("short", "long", "mixed")]
    public string RecordShape { get; set; } = "short";

    [Params("LF", "CRLF")]
    public string NewlineStyle { get; set; } = "LF";

    [GlobalSetup]
    public void Setup()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"novolis-ndjson-benchmark-{Guid.NewGuid():N}.ndjson");
        _file = new FileInfo(path);
        GenerateFile(_file, SizeMegabytes, RecordShape, NewlineStyle);

        _indexedDocument = new NdjsonFileReader(
                new NdjsonOpenOptions(IndexInterval, MaxTake: 1_000))
            .OpenAsync(_file)
            .GetAwaiter()
            .GetResult();
        _indexedDocument.RefreshAsync().GetAwaiter().GetResult();
        _recordCount = _indexedDocument.RecordCount;
        _baselineLength = _file.Length;
        _baselineRecordCount = _recordCount;
    }

    [IterationSetup(Target = nameof(IncrementalRefreshAsync))]
    public void ResetIncrementalFile()
    {
        Truncate(_file!, _baselineLength);
        _indexedDocument!.RefreshAsync().GetAwaiter().GetResult();
        _recordCount = _indexedDocument.RecordCount;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _indexedDocument?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (_file is { } file && file.Exists)
            file.Delete();
    }

    [Benchmark]
    public async Task<NdjsonSlice> IndexFullFileAsync()
    {
        await using var document = await new NdjsonFileReader().OpenAsync(_file!);
        await document.RefreshAsync();
        return await document.ReadAsync(take: 100);
    }

    [Benchmark]
    public async Task<NdjsonSlice> TimeToFirstSliceAsync()
    {
        await using var document = await new NdjsonFileReader().OpenAsync(_file!);
        return await document.ReadAsync(take: 100);
    }

    [Benchmark]
    public async Task<NdjsonSlice> RandomSliceSeekAsync()
    {
        var maximumSkip = Math.Max(1, _recordCount - 100);
        var skip = _random.NextInt64(0, maximumSkip);
        return await _indexedDocument!.ReadAsync(skip, 100);
    }

    [Benchmark]
    public async Task<long> IncrementalRefreshAsync()
    {
        AppendRecords(_file!, RecordShape, NewlineStyle, 1_000, _baselineRecordCount);
        await _indexedDocument!.RefreshAsync();
        return _indexedDocument.RecordCount;
    }

    private static void Truncate(FileInfo file, long length)
    {
        using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Write,
            FileShare.ReadWrite);
        stream.SetLength(length);
    }

    private static void GenerateFile(
        FileInfo file,
        int sizeMegabytes,
        string recordShape,
        string newlineStyle)
    {
        var ending = newlineStyle == "CRLF" ? "\r\n" : "\n";
        var targetBytes = (long)sizeMegabytes * 1024 * 1024;
        using var stream = new FileStream(
            file.FullName,
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                BufferSize = 64 * 1024,
                Options = FileOptions.SequentialScan,
            });

        var recordNumber = 0L;
        while (stream.Length < targetBytes)
        {
            var line = CreateLine(recordShape, recordNumber, ending);
            var bytes = Encoding.UTF8.GetBytes(line);
            stream.Write(bytes);
            recordNumber++;
        }
    }

    private static void AppendRecords(
        FileInfo file,
        string recordShape,
        string newlineStyle,
        int count,
        long startingRecord)
    {
        var ending = newlineStyle == "CRLF" ? "\r\n" : "\n";
        using var stream = new FileStream(
            file.FullName,
            new FileStreamOptions
            {
                Mode = FileMode.Append,
                Access = FileAccess.Write,
                Share = FileShare.ReadWrite,
                BufferSize = 64 * 1024,
                Options = FileOptions.SequentialScan,
            });
        for (var index = 0; index < count; index++)
        {
            var bytes = Encoding.UTF8.GetBytes(CreateLine(recordShape, startingRecord + index, ending));
            stream.Write(bytes);
        }
    }

    private static string CreateLine(
        string recordShape,
        long recordNumber,
        string ending)
    {
        var payload = recordShape switch
        {
            "long" => new string('x', 4_096),
            "mixed" when recordNumber % 3 == 0 => new string('x', 4_096),
            "mixed" when recordNumber % 3 == 1 => "brief",
            _ => "short",
        };
        return $"{{\"id\":{recordNumber},\"payload\":\"{payload}\"}}{ending}";
    }
}
