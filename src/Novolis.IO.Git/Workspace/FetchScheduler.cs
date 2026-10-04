namespace Novolis.IO.Git;

/// <summary>Periodic soft fetch across a checkout root (host Start/Stop only).</summary>
public sealed class FetchScheduler : IAsyncDisposable
{
    readonly GitRepositoryBatch _batch;
    CancellationTokenSource? _cts;
    Task? _loop;

    /// <summary>Creates a scheduler.</summary>
    public FetchScheduler(GitRepositoryService? git = null)
    {
        _batch = new GitRepositoryBatch(git ?? new GitRepositoryService());
    }

    /// <summary>Raised after each cycle with the batch result.</summary>
    public event EventHandler<BatchResult>? CycleCompleted;

    /// <summary>Raised on cycle errors.</summary>
    public event EventHandler<Exception>? CycleFailed;

    /// <summary>Whether the loop is running.</summary>
    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>Starts periodic fetch.</summary>
    /// <param name="workspaceRoot">Checkout root for discover + state.</param>
    /// <param name="interval">Delay between cycles (and before the first when <paramref name="delayBeforeFirst"/>).</param>
    /// <param name="filter">Optional repo filter.</param>
    /// <param name="parallel">Max parallel fetch degree.</param>
    /// <param name="delayBeforeFirst">When true (default), wait <paramref name="interval"/> before the first cycle so UI startup is not contested.</param>
    /// <param name="policy">Which children count as members. Default is every git child.</param>
    public void Start(
        string workspaceRoot,
        TimeSpan interval,
        RepoFilter? filter = null,
        int parallel = 6,
        bool delayBeforeFirst = true,
        GitDiscover policy = GitDiscover.GitChildren)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _loop = Task.Run(async () =>
        {
            if (delayBeforeFirst)
            {
                try
                {
                    await Task.Delay(interval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var forest = MultiGitRepositoryWorkspace.Discover(workspaceRoot, policy).Select(filter);
                    var result = await _batch.FetchAsync(forest, new BatchOptions
                    {
                        Parallel = parallel,
                        WorkspaceRoot = workspaceRoot,
                    }, ct).ConfigureAwait(false);
                    CycleCompleted?.Invoke(this, result);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    CycleFailed?.Invoke(this, ex);
                }

                try
                {
                    await Task.Delay(interval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, ct);
    }

    /// <summary>Stops the loop.</summary>
    public void Stop()
    {
        if (_cts is null)
            return;
        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignore
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch { /* ignore */ }
        }
    }
}
