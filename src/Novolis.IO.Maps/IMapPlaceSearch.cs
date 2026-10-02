namespace Novolis.IO.Maps;

/// <summary>Searches human-entered place text independently from tile imagery.</summary>
public interface IMapPlaceSearch
{
    /// <summary>Returns provider places matching <paramref name="query"/>.</summary>
    Task<IReadOnlyList<MapPlace>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
