namespace Novolis.IO.Git;

public sealed partial class GitRepositoryService
{
    /// <summary>Builds a status matrix (parallel git probes; safe to call off the UI thread).</summary>
    public GitStatusMatrix GetStatusMatrix(
        MultiGitRepositoryWorkspace forest,
        RepoFilter? filter = null,
        RepoStateStore? state = null,
        bool includeStashCount = true,
        int parallel = 8)
    {
        return GetStatusMatrixAsync(forest, filter, state, includeStashCount, parallel, liteStatus: true)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Async status matrix with bounded parallelism.</summary>
    public async Task<GitStatusMatrix> GetStatusMatrixAsync(
        MultiGitRepositoryWorkspace forest,
        RepoFilter? filter = null,
        RepoStateStore? state = null,
        bool includeStashCount = true,
        int parallel = 8,
        bool liteStatus = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forest);
        await Task.Yield();
        var root = Path.GetFullPath(forest.Root.FullName);
        state ??= await Task.Run(() => RepoStateStore.Load(root), cancellationToken).ConfigureAwait(false);
        var selected = forest.Select(filter).Members;
        var degree = Math.Clamp(parallel, 1, 32);
        var bag = new System.Collections.Concurrent.ConcurrentBag<RepoStatusRow>();

        await Parallel.ForEachAsync(
            selected,
            new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken },
            (repo, _) =>
            {
                var path = repo.Root.FullName;
                try
                {
                    var status = GetStatus(path, lite: liteStatus);
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
                        stashCount = ListStashes(path).Count;

                    bag.Add(new RepoStatusRow
                    {
                        Repo = repo,
                        Status = status,
                        StashCount = stashCount,
                        LastFetchAt = state.GetLastFetch(repo.RepositoryName),
                    });
                }
                catch (Exception ex)
                {
                    bag.Add(new RepoStatusRow
                    {
                        Repo = repo,
                        Error = ex.Message,
                        LastFetchAt = state.GetLastFetch(repo.RepositoryName),
                    });
                }

                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        var rows = bag.OrderBy(r => r.Repo.RepositoryName, StringComparer.OrdinalIgnoreCase).ToArray();
        return new GitStatusMatrix
        {
            Root = root,
            FetchedAt = DateTimeOffset.UtcNow,
            Repos = rows,
            Summary = new GitStatusSummary
            {
                Total = rows.Length,
                Git = rows.Count(r => r.Status is not null),
                Dirty = rows.Count(r => r.Status?.Dirty == true),
                Behind = rows.Count(r => (r.Status?.Behind ?? 0) > 0),
                Ahead = rows.Count(r => (r.Status?.Ahead ?? 0) > 0),
            },
        };
    }
}
