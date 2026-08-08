namespace Novolis.IO.Git;

/// <summary>Per-repo batch outcome.</summary>
public sealed class BatchRepoResult
{
    /// <summary>Repo.</summary>
    public required RepoEntry Repo { get; init; }

    /// <summary>ok | skipped | failed.</summary>
    public required string Outcome { get; init; }

    /// <summary>Message.</summary>
    public required string Message { get; init; }

    /// <summary>Optional planned argv for dry-run.</summary>
    public IReadOnlyList<string>? PlannedArgs { get; init; }
}

/// <summary>Batch operation result.</summary>
public sealed class BatchResult
{
    /// <summary>Per-repo results.</summary>
    public IReadOnlyList<BatchRepoResult> Results { get; init; } = [];

    /// <summary>True when every non-skipped repo succeeded.</summary>
    public bool Ok => Results.All(r => r.Outcome is "ok" or "skipped");

    /// <summary>Any failures.</summary>
    public bool HasFailures => Results.Any(r => r.Outcome == "failed");
}

/// <summary>Batch options.</summary>
public sealed class BatchOptions
{
    /// <summary>Max parallel git processes.</summary>
    public int Parallel { get; init; } = 6;

    /// <summary>When true, skip dirty repos on mutate.</summary>
    public bool SkipDirty { get; init; } = true;

    /// <summary>Dry-run (no mutate).</summary>
    public bool DryRun { get; init; }

    /// <summary>Workspace root for locks/state.</summary>
    public string? WorkspaceRoot { get; init; }
}

/// <summary>Parallel fetch / pull / checkout across repos.</summary>
public sealed class GitWorkspaceBatch
{
    readonly GitRepositoryService _git;

    /// <summary>Creates a batch runner.</summary>
    public GitWorkspaceBatch(GitRepositoryService? git = null)
    {
        _git = git ?? new GitRepositoryService();
    }

    /// <summary>Fetches many repos (no merge).</summary>
    public async Task<BatchResult> FetchAsync(
        IReadOnlyList<RepoEntry> repos,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchOptions();
        return await RunAsync(repos, options, exclusive: false, async (repo, ct) =>
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

            var r = _git.Fetch(repo.Path);
            if (r.Ok && options.WorkspaceRoot is not null)
                RepoStateStore.Load(options.WorkspaceRoot).SetLastFetch(repo.Name, DateTimeOffset.UtcNow);
            return new BatchRepoResult
            {
                Repo = repo,
                Outcome = r.Ok ? "ok" : "failed",
                Message = r.Message,
            };
        }, cancellationToken);
    }

    /// <summary>Fast-forward pull many repos.</summary>
    public Task<BatchResult> PullFfOnlyAsync(
        IReadOnlyList<RepoEntry> repos,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchOptions();
        return RunAsync(repos, options, exclusive: true, (repo, ct) =>
        {
            if (options.SkipDirty)
            {
                var status = _git.GetStatus(repo.Path);
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

            var r = _git.PullFfOnly(repo.Path, new PullOptions { FfOnly = true });
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
        IReadOnlyList<RepoEntry> repos,
        string refName,
        BatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BatchOptions();
        return RunAsync(repos, options, exclusive: true, (repo, ct) =>
        {
            if (options.SkipDirty)
            {
                var status = _git.GetStatus(repo.Path);
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

            var r = _git.Checkout(repo.Path, refName);
            return Task.FromResult(new BatchRepoResult
            {
                Repo = repo,
                Outcome = r.Ok ? "ok" : "failed",
                Message = r.Message,
            });
        }, cancellationToken);
    }

    async Task<BatchResult> RunAsync(
        IReadOnlyList<RepoEntry> repos,
        BatchOptions options,
        bool exclusive,
        Func<RepoEntry, CancellationToken, Task<BatchRepoResult>> work,
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
                        ? RepoLock.TryAcquireExclusive(options.WorkspaceRoot, repo.Name)
                        : RepoLock.TryAcquireShared(options.WorkspaceRoot, repo.Name);
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
}
