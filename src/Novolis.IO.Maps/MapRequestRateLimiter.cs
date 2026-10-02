namespace Novolis.IO.Maps;

internal sealed class MapRequestRateLimiter
{
    readonly SemaphoreSlim _gate = new(1, 1);
    long? _lastRequestTimestamp;

    public async ValueTask WaitAsync(
        TimeSpan minimumInterval,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetTimestamp();
            if (_lastRequestTimestamp is { } lastRequest)
            {
                var remaining = minimumInterval
                    - timeProvider.GetElapsedTime(lastRequest, now);
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(
                        remaining,
                        timeProvider,
                        cancellationToken);
                }
            }

            _lastRequestTimestamp = timeProvider.GetTimestamp();
        }
        finally
        {
            _gate.Release();
        }
    }
}
