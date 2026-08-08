namespace Novolis.IO.Git;

/// <summary>Planned branch-cut for one repo.</summary>
public sealed class BranchCutRepoStep
{
    /// <summary>Repo.</summary>
    public required RepoEntry Repo { get; init; }

    /// <summary>Planned git argv.</summary>
    public required IReadOnlyList<string> PlannedArgs { get; init; }

    /// <summary>Block reason if not applicable.</summary>
    public string? BlockReason { get; init; }
}

/// <summary>A branch-cut plan.</summary>
public sealed class BranchPlan
{
    /// <summary>Plan id.</summary>
    public required string Id { get; init; }

    /// <summary>Branch name.</summary>
    public required string Name { get; init; }

    /// <summary>Base ref.</summary>
    public required string BaseRef { get; init; }

    /// <summary>Workspace root.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Steps.</summary>
    public IReadOnlyList<BranchCutRepoStep> Steps { get; init; } = [];

    /// <summary>Created UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Apply outcome for a plan.</summary>
public sealed class BranchPlanResult
{
    /// <summary>Plan id.</summary>
    public required string PlanId { get; init; }

    /// <summary>Dry-run flag.</summary>
    public bool DryRun { get; init; }

    /// <summary>Batch-style results.</summary>
    public IReadOnlyList<BatchRepoResult> Results { get; init; } = [];

    /// <summary>Overall ok.</summary>
    public bool Ok => Results.All(r => r.Outcome is "ok" or "skipped");
}

/// <summary>Plans and applies the same feature branch across many repos.</summary>
public sealed class BranchCutPlanner
{
    static readonly Dictionary<string, BranchPlan> Plans = new(StringComparer.OrdinalIgnoreCase);
    readonly GitRepositoryService _git;

    /// <summary>Creates a planner.</summary>
    public BranchCutPlanner(GitRepositoryService? git = null)
    {
        _git = git ?? new GitRepositoryService();
    }

    /// <summary>Builds a plan (blocks dirty / detached unless forceDirty).</summary>
    public BranchPlan Plan(
        string workspaceRoot,
        string branchName,
        IReadOnlyList<RepoEntry> repos,
        string baseRef = "main",
        bool forceDirty = false)
    {
        if (string.IsNullOrWhiteSpace(branchName))
            throw new ArgumentException("Branch name is required.", nameof(branchName));

        var steps = new List<BranchCutRepoStep>();
        foreach (var repo in repos)
        {
            string? block = null;
            try
            {
                var status = _git.GetStatus(repo.Path);
                if (status.Dirty && !forceDirty)
                    block = "dirty worktree";
                if (string.Equals(status.Branch, "HEAD", StringComparison.Ordinal))
                    block = "detached HEAD";
            }
            catch (Exception ex)
            {
                block = ex.Message;
            }

            steps.Add(new BranchCutRepoStep
            {
                Repo = repo,
                PlannedArgs = ["checkout", "-B", branchName, baseRef],
                BlockReason = block,
            });
        }

        var plan = new BranchPlan
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = branchName,
            BaseRef = baseRef,
            WorkspaceRoot = Path.GetFullPath(workspaceRoot),
            Steps = steps,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Plans[plan.Id] = plan;
        return plan;
    }

    /// <summary>Retrieves a plan by id.</summary>
    public BranchPlan? GetPlan(string planId) =>
        Plans.TryGetValue(planId, out var p) ? p : null;

    /// <summary>Applies a plan.</summary>
    public async Task<BranchPlanResult> ApplyAsync(
        BranchPlan plan,
        bool dryRun = false,
        int parallel = 4,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BatchRepoResult>();
        var batch = new GitWorkspaceBatch(_git);
        var applicable = plan.Steps.Where(s => s.BlockReason is null).Select(s => s.Repo).ToArray();
        foreach (var blocked in plan.Steps.Where(s => s.BlockReason is not null))
        {
            results.Add(new BatchRepoResult
            {
                Repo = blocked.Repo,
                Outcome = "skipped",
                Message = blocked.BlockReason!,
                PlannedArgs = blocked.PlannedArgs,
            });
        }

        if (applicable.Length == 0)
        {
            return new BranchPlanResult { PlanId = plan.Id, DryRun = dryRun, Results = results };
        }

        if (dryRun)
        {
            foreach (var step in plan.Steps.Where(s => s.BlockReason is null))
            {
                results.Add(new BatchRepoResult
                {
                    Repo = step.Repo,
                    Outcome = "ok",
                    Message = "dry-run",
                    PlannedArgs = step.PlannedArgs,
                });
            }

            return new BranchPlanResult { PlanId = plan.Id, DryRun = true, Results = results };
        }

        var opts = new BatchOptions
        {
            Parallel = parallel,
            SkipDirty = true,
            DryRun = false,
            WorkspaceRoot = plan.WorkspaceRoot,
        };

        // CreateBranch per repo via exclusive batch-like loop
        using var gate = new SemaphoreSlim(Math.Clamp(parallel, 1, 32));
        var tasks = applicable.Select(async repo =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var lockHandle = RepoLock.TryAcquireExclusive(plan.WorkspaceRoot, repo.Name);
                if (lockHandle is null)
                {
                    return new BatchRepoResult
                    {
                        Repo = repo,
                        Outcome = "failed",
                        Message = "could not acquire repo lock",
                    };
                }

                var r = _git.CreateBranch(repo.Path, new CreateBranchOptions
                {
                    Name = plan.Name,
                    BaseRef = plan.BaseRef,
                    Checkout = true,
                });
                return new BatchRepoResult
                {
                    Repo = repo,
                    Outcome = r.Ok ? "ok" : "failed",
                    Message = r.Message,
                    PlannedArgs = ["checkout", "-B", plan.Name, plan.BaseRef],
                };
            }
            finally
            {
                gate.Release();
            }
        });

        results.AddRange(await Task.WhenAll(tasks).ConfigureAwait(false));
        _ = batch;
        return new BranchPlanResult { PlanId = plan.Id, DryRun = false, Results = results };
    }
}
