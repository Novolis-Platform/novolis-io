namespace Novolis.IO.SystemMetrics;

internal sealed class OnlineMetricStatistics
{
    private const int ReservoirCapacity = 1024;
    private readonly List<double> _reservoir = new(ReservoirCapacity);
    private long _count;
    private double _minimum = double.PositiveInfinity;
    private double _maximum = double.NegativeInfinity;
    private double _sum;

    public void Add(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return;

        _count++;
        _minimum = Math.Min(_minimum, value);
        _maximum = Math.Max(_maximum, value);
        _sum += value;

        if (_reservoir.Count < ReservoirCapacity)
        {
            _reservoir.Add(value);
            return;
        }

        var index = Random.Shared.NextInt64(_count);
        if (index < ReservoirCapacity)
            _reservoir[(int)index] = value;
    }

    public MetricStatistics? Build()
    {
        if (_count == 0)
            return null;

        var values = _reservoir.ToArray();
        Array.Sort(values);
        return new MetricStatistics(
            _count,
            _minimum,
            _maximum,
            _sum / _count,
            Percentile(values, 0.50),
            Percentile(values, 0.95));
    }

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 1)
            return values[0];

        var position = (values.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return values[lower];

        var fraction = position - lower;
        return values[lower] + (values[upper] - values[lower]) * fraction;
    }
}
