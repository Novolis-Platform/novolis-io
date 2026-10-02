using System.Diagnostics;

namespace Novolis.IO.Maps;

internal sealed class MapRequestRateLimiter
{
    readonly SemaphoreSlim _gate = new(1, 1);
    long _lastRequestTimestamp;

    public async ValueTask WaitAsync(
        TimeSpan minimumInterval,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var intervalTicks = (long)(
                minimumInterval.TotalSeconds * Stopwatch.Frequency);
            var now = Stopwatch.GetTimestamp();
            var remaining = _lastRequestTimestamp + intervalTicks - now;
            if (remaining > 0)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds((double)remaining / Stopwatch.Frequency),
                    cancellationToken);
            }

            _lastRequestTimestamp = Stopwatch.GetTimestamp();
        }
        finally
        {
            _gate.Release();
        }
    }
}
