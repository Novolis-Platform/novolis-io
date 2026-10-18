namespace Novolis.IO.SystemMetrics;

internal sealed class CaptureStatistics
{
    private readonly OnlineMetricStatistics _cpu = new();
    private readonly OnlineMetricStatistics _processCpu = new();
    private readonly Dictionary<string, OnlineMetricStatistics> _gpu = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OnlineMetricStatistics> _receive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OnlineMetricStatistics> _send = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly List<SystemMetricDiagnostic> _diagnostics = [];
    private long _sampleCount;

    public void Add(SystemMetricsSample sample)
    {
        lock (_gate)
        {
            _sampleCount++;
            Add(_cpu, sample.Cpu.UtilizationPercent);
            Add(_processCpu, sample.Process?.CpuUtilizationPercent);

            foreach (var gpu in sample.Gpus)
                Add(Get(_gpu, gpu.AdapterId), gpu.UtilizationPercent);
            foreach (var network in sample.Networks)
            {
                Add(Get(_receive, network.AdapterId), network.ReceiveBytesPerSecond);
                Add(Get(_send, network.AdapterId), network.SendBytesPerSecond);
            }

            _diagnostics.AddRange(sample.Diagnostics);
        }
    }

    public void AddDiagnostic(SystemMetricDiagnostic diagnostic)
    {
        lock (_gate)
            _diagnostics.Add(diagnostic);
    }

    public void AddDiagnostics(IEnumerable<SystemMetricDiagnostic> diagnostics)
    {
        lock (_gate)
            _diagnostics.AddRange(diagnostics);
    }

    public SystemMetricsSummary Build(
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        IReadOnlyList<SystemMetricsMarker> markers)
    {
        lock (_gate)
        {
            return new SystemMetricsSummary(
                startedAt,
                endedAt,
                endedAt - startedAt,
                _sampleCount,
                _cpu.Build(),
                _processCpu.Build(),
                Build(_gpu),
                Build(_receive),
                Build(_send),
                markers,
                _diagnostics.ToArray());
        }
    }

    private static void Add(OnlineMetricStatistics statistics, double? value)
    {
        if (value is { } number)
            statistics.Add(number);
    }

    private static void Add(OnlineMetricStatistics statistics, long? value)
    {
        if (value is { } number)
            statistics.Add(number);
    }

    private static OnlineMetricStatistics Get(
        Dictionary<string, OnlineMetricStatistics> values,
        string key)
    {
        if (!values.TryGetValue(key, out var statistics))
        {
            statistics = new OnlineMetricStatistics();
            values.Add(key, statistics);
        }

        return statistics;
    }

    private static IReadOnlyDictionary<string, MetricStatistics> Build(
        Dictionary<string, OnlineMetricStatistics> values) =>
        values
            .Select(pair => new
            {
                pair.Key,
                Statistics = pair.Value.Build(),
            })
            .Where(pair => pair.Statistics is not null)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Statistics!,
                StringComparer.Ordinal);
}
