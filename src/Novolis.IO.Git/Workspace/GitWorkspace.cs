namespace Novolis.IO.Git;

/// <summary>One discovered repository under a workspace root.</summary>
public sealed class RepoEntry
{
    /// <summary>Folder name (e.g. novolis-io).</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path.</summary>
    public required string Path { get; init; }

    /// <summary>Whether a .git directory/file exists.</summary>
    public bool IsGit { get; init; }
}

/// <summary>Filter for workspace repo selection.</summary>
public sealed class RepoFilter
{
    /// <summary>Include only these names (short or novolis-*); empty = all.</summary>
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>Exclude these names.</summary>
    public IReadOnlyList<string>? Exclude { get; init; }

    /// <summary>Only dirty repos.</summary>
    public bool? Dirty { get; init; }

    /// <summary>Only repos behind upstream.</summary>
    public bool? Behind { get; init; }

    /// <summary>Only repos ahead of upstream.</summary>
    public bool? Ahead { get; init; }

    /// <summary>Only repos on this branch name.</summary>
    public string? OnBranch { get; init; }
}

/// <summary>Status row for one repo.</summary>
public sealed class RepoStatusRow
{
    /// <summary>Repo entry.</summary>
    public required RepoEntry Repo { get; init; }

    /// <summary>Status when git; null when not.</summary>
    public GitStatus? Status { get; init; }

    /// <summary>Stash count when known.</summary>
    public int StashCount { get; init; }

    /// <summary>Last fetch UTC (from state store).</summary>
    public DateTimeOffset? LastFetchAt { get; init; }

    /// <summary>Error reading status.</summary>
    public string? Error { get; init; }
}

/// <summary>Workspace status matrix.</summary>
public sealed class WorkspaceStatusMatrix
{
    /// <summary>Absolute workspace root.</summary>
    public required string Root { get; init; }

    /// <summary>When matrix was built (UTC).</summary>
    public DateTimeOffset FetchedAt { get; init; }

    /// <summary>Rows.</summary>
    public IReadOnlyList<RepoStatusRow> Repos { get; init; } = [];

    /// <summary>Summary counts.</summary>
    public WorkspaceStatusSummary Summary { get; init; } = new();
}

/// <summary>Aggregate counts.</summary>
public sealed class WorkspaceStatusSummary
{
    /// <summary>Total repos listed.</summary>
    public int Total { get; init; }

    /// <summary>Git repos.</summary>
    public int Git { get; init; }

    /// <summary>Dirty.</summary>
    public int Dirty { get; init; }

    /// <summary>Behind.</summary>
    public int Behind { get; init; }

    /// <summary>Ahead.</summary>
    public int Ahead { get; init; }
}

/// <summary>Multi-select of repos.</summary>
public sealed class RepoSelection
{
    /// <summary>Workspace root.</summary>
    public required string Root { get; init; }

    /// <summary>Selected repos.</summary>
    public IReadOnlyList<RepoEntry> Selected { get; init; } = [];
}

/// <summary>Discovers Novolis workspace roots and status matrices.</summary>
public static class GitWorkspace
{
    /// <summary>Resolves org root from explicit path, NOVOLIS_ROOT, or walk for markers.</summary>
    public static string ResolveRoot(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        var env = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "Novolis.Platform.slnx");
            var gov = Path.Combine(dir.FullName, "novolis-governance");
            if (File.Exists(marker) || Directory.Exists(gov))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not resolve Novolis workspace root. Pass an explicit root or set NOVOLIS_ROOT.");
    }

    /// <summary>Discovers novolis-* folders (and any child with .git when pattern matches).</summary>
    public static IReadOnlyList<RepoEntry> Discover(string root)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);

        var list = new List<RepoEntry>();
        foreach (var dir in Directory.EnumerateDirectories(root, "novolis-*"))
        {
            var name = Path.GetFileName(dir);
            var gitDir = Path.Combine(dir, ".git");
            list.Add(new RepoEntry
            {
                Name = name,
                Path = Path.GetFullPath(dir),
                IsGit = Directory.Exists(gitDir) || File.Exists(gitDir),
            });
        }

        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    /// <summary>Applies name include/exclude before status probing.</summary>
    public static IReadOnlyList<RepoEntry> SelectByNames(IReadOnlyList<RepoEntry> repos, RepoFilter? filter)
    {
        filter ??= new RepoFilter();
        IEnumerable<RepoEntry> q = repos.Where(r => r.IsGit);
        if (filter.Include is { Count: > 0 })
        {
            var set = NormalizeNames(filter.Include);
            q = q.Where(r => set.Contains(r.Name) || set.Contains(StripPrefix(r.Name)));
        }

        if (filter.Exclude is { Count: > 0 })
        {
            var set = NormalizeNames(filter.Exclude);
            q = q.Where(r => !set.Contains(r.Name) && !set.Contains(StripPrefix(r.Name)));
        }

        return q.ToArray();
    }

    /// <summary>Builds a status matrix (parallel git probes; safe to call off the UI thread).</summary>
    public static WorkspaceStatusMatrix GetStatusMatrix(
        string root,
        GitRepositoryService git,
        RepoFilter? filter = null,
        RepoStateStore? state = null,
        bool includeStashCount = true,
        int parallel = 8)
    {
        return GetStatusMatrixAsync(root, git, filter, state, includeStashCount, parallel)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Async status matrix with bounded parallelism.</summary>
    public static async Task<WorkspaceStatusMatrix> GetStatusMatrixAsync(
        string root,
        GitRepositoryService git,
        RepoFilter? filter = null,
        RepoStateStore? state = null,
        bool includeStashCount = true,
        int parallel = 8,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        root = Path.GetFullPath(root);
        state ??= RepoStateStore.Load(root);
        var discovered = Discover(root);
        var selected = SelectByNames(discovered, filter);
        var degree = Math.Clamp(parallel, 1, 32);
        var bag = new System.Collections.Concurrent.ConcurrentBag<RepoStatusRow>();

        await Parallel.ForEachAsync(
            selected,
            new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken },
            (repo, ct) =>
            {
                try
                {
                    var status = git.GetStatus(repo.Path);
                    if (filter?.Dirty is true && !status.Dirty)
                        return ValueTask.CompletedTask;
                    if (filter?.Dirty is false && status.Dirty)
                        return ValueTask.CompletedTask;
                    if (filter?.Behind is true && status.Behind <= 0)
                        return ValueTask.CompletedTask;
                    if (filter?.Ahead is true && status.Ahead <= 0)
                        return ValueTask.CompletedTask;
                    if (!string.IsNullOrWhiteSpace(filter?.OnBranch)
                        && !string.Equals(status.Branch, filter.OnBranch, StringComparison.Ordinal))
                        return ValueTask.CompletedTask;

                    var stashCount = 0;
                    if (includeStashCount)
                        stashCount = git.ListStashes(repo.Path).Count;

                    bag.Add(new RepoStatusRow
                    {
                        Repo = repo,
                        Status = status,
                        StashCount = stashCount,
                        LastFetchAt = state.GetLastFetch(repo.Name),
                    });
                }
                catch (Exception ex)
                {
                    bag.Add(new RepoStatusRow
                    {
                        Repo = repo,
                        Error = ex.Message,
                        LastFetchAt = state.GetLastFetch(repo.Name),
                    });
                }

                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        var rows = bag.OrderBy(r => r.Repo.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var summary = new WorkspaceStatusSummary
        {
            Total = rows.Length,
            Git = rows.Count(r => r.Status is not null),
            Dirty = rows.Count(r => r.Status?.Dirty == true),
            Behind = rows.Count(r => (r.Status?.Behind ?? 0) > 0),
            Ahead = rows.Count(r => (r.Status?.Ahead ?? 0) > 0),
        };

        return new WorkspaceStatusMatrix
        {
            Root = root,
            FetchedAt = DateTimeOffset.UtcNow,
            Repos = rows,
            Summary = summary,
        };
    }

    static HashSet<string> NormalizeNames(IEnumerable<string> names)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in names)
        {
            if (string.IsNullOrWhiteSpace(n))
                continue;
            foreach (var part in n.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                set.Add(part);
                set.Add(part.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase)
                    ? part
                    : "novolis-" + part);
                set.Add(StripPrefix(part));
            }
        }

        return set;
    }

    static string StripPrefix(string name) =>
        name.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase) ? name["novolis-".Length..] : name;
}
