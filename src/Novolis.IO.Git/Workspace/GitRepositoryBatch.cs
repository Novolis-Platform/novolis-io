namespace Novolis.IO.Git;

/// <summary>Parallel fetch / pull / checkout across repos.</summary>
public sealed class GitRepositoryBatch
{
    readonly GitRepositoryService _git;

    /// <summary>Creates a batch runner.</summary>
    public GitRepositoryBatch(GitRepositoryService? git = null)
    {
        _git = git ?? new GitRepositoryService();
    }

    /// <summary>Fetches many repos (no merge).</summary>
    public Task<BatchResult> FetchAsync(
        MultiGitRepositoryWorkspace forest,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(forest);
        options ??= new BatchOptions();
        options = WithRoot(options, forest.Root.FullName);
        return FetchAsync(forest.Members, options, cancellationToken);
    }

    /// <summary>Fetches many repos (no merge).</summary>
    public async Task<BatchResult> FetchAsync(
        IReadOnlyList<GitRepositoryWorkspace> repos,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchOptions();
        return await RunAsync(repos, options, exclusive: false, async (repo, _) =>
        {
            if (options.DryRun)
            {
                return new BatchRepoResult
                {
                    Repo = repo,
                    Outcome = "ok",
                    Message = "dry-run fetch",
                    PlannedArgs = ["fetch", "origin", "--prune"],
                };
            }

            var path = repo.Root.FullName;
            var r = _git.Fetch(path);
            if (r.Ok && options.WorkspaceRoot is not null)
                RepoStateStore.Load(options.WorkspaceRoot).SetLastFetch(repo.RepositoryName, DateTimeOffset.UtcNow);
            return new BatchRepoResult
            {
                Repo = repo,
                Outcome = r.Ok ? "ok" : "failed",
                Message = r.Message,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fast-forward pull many repos.</summary>
    public Task<BatchResult> PullFfOnlyAsync(
        IReadOnlyList<GitRepositoryWorkspace> repos,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchOptions();
        return RunAsync(repos, options, exclusive: true, (repo, _) =>
        {
            var path = repo.Root.FullName;
            if (options.SkipDirty)
            {
                var status = _git.GetStatus(path);
                if (status.Dirty)
                {
                    return Task.FromResult(new BatchRepoResult
                    {
                        Repo = repo,
                        Outcome = "skipped",
                        Message = "dirty worktree",
                    });
                }
            }

            if (options.DryRun)
            {
                return Task.FromResult(new BatchRepoResult
                {
                    Repo = repo,
                    Outcome = "ok",
                    Message = "dry-run pull --ff-only",
                    PlannedArgs = ["pull", "origin", "--ff-only"],
                });
            }

            var r = _git.PullFfOnly(path, new PullOptions { FfOnly = true });
            return Task.FromResult(new BatchRepoResult
            {
                Repo = repo,
                Outcome = r.Ok ? "ok" : "failed",
                Message = r.Message,
            });
        }, cancellationToken);
    }

    /// <summary>Checkout the same ref across repos.</summary>
    public Task<BatchResult> CheckoutAsync(
        IReadOnlyList<GitRepositoryWorkspace> repos,
        string refName,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchOptions();
        return RunAsync(repos, options, exclusive: true, (repo, _) =>
        {
            var path = repo.Root.FullName;
            if (options.SkipDirty)
            {
                var status = _git.GetStatus(path);
                if (status.Dirty)
                {
                    return Task.FromResult(new BatchRepoResult
                    {
                        Repo = repo,
                        Outcome = "skipped",
                        Message = "dirty worktree",
                    });
                }
            }

            if (options.DryRun)
            {
                return Task.FromResult(new BatchRepoResult
                {
                    Repo = repo,
                    Outcome = "ok",
                    Message = $"dry-run checkout {refName}",
                    PlannedArgs = ["checkout", refName],
                });
            }

            var r = _git.Checkout(path, refName);
            return Task.FromResult(new BatchRepoResult
            {
                Repo = repo,
                Outcome = r.Ok ? "ok" : "failed",
                Message = r.Message,
            });
        }, cancellationToken);
    }

    async Task<BatchResult> RunAsync(
        IReadOnlyList<GitRepositoryWorkspace> repos,
        BatchOptions options,
        bool exclusive,
        Func<GitRepositoryWorkspace, CancellationToken, Task<BatchRepoResult>> work,
        CancellationToken cancellationToken)
    {
        var degree = Math.Clamp(options.Parallel, 1, 32);
        using var gate = new SemaphoreSlim(degree, degree);
        var tasks = repos.Select(async repo =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                RepoLock? lockHandle = null;
                if (options.WorkspaceRoot is not null)
                {
                    lockHandle = exclusive
                        ? RepoLock.TryAcquireExclusive(options.WorkspaceRoot, repo.RepositoryName)
                        : RepoLock.TryAcquireShared(options.WorkspaceRoot, repo.RepositoryName);
                    if (lockHandle is null)
                    {
                        return new BatchRepoResult
                        {
                            Repo = repo,
                            Outcome = "failed",
                            Message = "could not acquire repo lock",
                        };
                    }
                }

                using (lockHandle)
                    return await work(repo, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new BatchRepoResult
                {
                    Repo = repo,
                    Outcome = "failed",
                    Message = ex.Message,
                };
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return new BatchResult { Results = results };
    }

    static BatchOptions WithRoot(BatchOptions options, string root) =>
        options.WorkspaceRoot is null
            ? new BatchOptions
            {
                Parallel = options.Parallel,
                SkipDirty = options.SkipDirty,
                DryRun = options.DryRun,
                WorkspaceRoot = root,
            }
            : options;
}
