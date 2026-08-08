using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.IO.Git;

/// <summary>Persists last-fetch timestamps under workspace <c>.novolis/repos-state.json</c>.</summary>
public sealed class RepoStateStore
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    readonly string _path;
    readonly Dictionary<string, DateTimeOffset> _lastFetch = new(StringComparer.OrdinalIgnoreCase);

    RepoStateStore(string path)
    {
        _path = path;
    }

    /// <summary>Loads or creates state for a workspace root.</summary>
    public static RepoStateStore Load(string workspaceRoot)
    {
        var path = Path.Combine(workspaceRoot, ".novolis", "repos-state.json");
        var store = new RepoStateStore(path);
        if (!File.Exists(path))
            return store;

        try
        {
            var dto = JsonSerializer.Deserialize<StateFile>(File.ReadAllText(path), JsonOpts);
            if (dto?.LastFetch is not null)
            {
                foreach (var (k, v) in dto.LastFetch)
                {
                    if (DateTimeOffset.TryParse(v, out var dtoff))
                        store._lastFetch[k] = dtoff;
                }
            }
        }
        catch
        {
            // corrupt state — start fresh
        }

        return store;
    }

    /// <summary>Last fetch for a repo name, if any.</summary>
    public DateTimeOffset? GetLastFetch(string repoName) =>
        _lastFetch.TryGetValue(repoName, out var v) ? v : null;

    /// <summary>Records a successful fetch time.</summary>
    public void SetLastFetch(string repoName, DateTimeOffset when)
    {
        _lastFetch[repoName] = when;
        Save();
    }

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var dto = new StateFile
        {
            LastFetch = _lastFetch.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToString("o"),
                StringComparer.OrdinalIgnoreCase),
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(dto, JsonOpts));
    }

    sealed class StateFile
    {
        public Dictionary<string, string>? LastFetch { get; set; }
    }
}
