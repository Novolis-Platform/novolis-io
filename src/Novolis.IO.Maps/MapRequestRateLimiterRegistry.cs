using System.Collections.Concurrent;

namespace Novolis.IO.Maps;

internal static class MapRequestRateLimiterRegistry
{
    static readonly ConcurrentDictionary<string, MapRequestRateLimiter> Limiters =
        new(StringComparer.Ordinal);

    public static ValueTask WaitAsync(
        string provider,
        TimeSpan minimumInterval,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        Limiters
            .GetOrAdd(provider, static _ => new MapRequestRateLimiter())
            .WaitAsync(minimumInterval, timeProvider, cancellationToken);
}
