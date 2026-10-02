namespace Novolis.IO.Maps;

/// <summary>Resource and freshness limits for an XYZ tile source.</summary>
public sealed class XyzMapSourceOptions
{
    /// <summary>Maximum bytes retained in this source's disk cache.</summary>
    public long MaximumCacheBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>Maximum number of files retained in this source's disk cache.</summary>
    public int MaximumCachedTiles { get; init; } = 4_096;

    /// <summary>Maximum age of a cached tile used after a refresh failure.</summary>
    public TimeSpan MaximumStaleAge { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Maximum encoded response size accepted from a provider.</summary>
    public int MaximumTileBytes { get; init; } = 4_000_000;

    /// <summary>Maximum simultaneous provider requests for this source.</summary>
    public int MaximumConcurrentRequests { get; init; } = 6;

    /// <summary>Minimum interval between requests to the same tile provider.</summary>
    public TimeSpan MinimumRequestInterval { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Minimum interval between cache eviction scans.</summary>
    public TimeSpan MinimumEvictionInterval { get; init; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (MaximumCacheBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumCacheBytes));
        if (MaximumCachedTiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumCachedTiles));
        if (MaximumStaleAge < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaximumStaleAge));
        if (MaximumTileBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumTileBytes));
        if (MaximumConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumConcurrentRequests));
        if (MinimumRequestInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MinimumRequestInterval));
        if (MinimumEvictionInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MinimumEvictionInterval));
    }
}
