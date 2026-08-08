namespace Novolis.IO.Git;

/// <summary>Periodic soft fetch across a workspace (host Start/Stop only).</summary>
public sealed class FetchScheduler : IAsyncDisposable
{
    readonly GitRepositoryService _git;
    readonly GitWorkspaceBatch _batch;
    CancellationTokenSource? _cts;
    Task? _loop;

    /// <summary>Creates a scheduler.</summary>
    public FetchScheduler(GitRepositoryService? git = null)
    {
        _git = git ?? new GitRepositoryService();
        _batch = new GitWorkspaceBatch(_git);
    }

    /// <summary>Raised after each cycle with the batch result.</summary>
    public event EventHandler<BatchResult>? CycleCompleted;

    /// <summary>Raised on cycle errors.</summary>
    public event EventHandler<Exception>? CycleFailed;

    /// <summary>Whether the loop is running.</summary>
    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>Starts periodic fetch.</summary>
    public void Start(string workspaceRoot, TimeSpan interval, RepoFilter? filter = null, int parallel = 6)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _loop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var repos = GitWorkspace.SelectByNames(GitWorkspace.Discover(workspaceRoot), filter);
                    var result = await _batch.FetchAsync(repos, new BatchOptions
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
