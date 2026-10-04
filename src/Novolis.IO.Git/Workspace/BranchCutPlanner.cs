namespace Novolis.IO.Git;

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
        IReadOnlyList<GitRepositoryWorkspace> repos,
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
                var status = _git.GetStatus(repo.Root.FullName);
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

        using var gate = new SemaphoreSlim(Math.Clamp(parallel, 1, 32));
        var tasks = applicable.Select(async repo =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var lockHandle = RepoLock.TryAcquireExclusive(plan.WorkspaceRoot, repo.RepositoryName);
                if (lockHandle is null)
                {
                    return new BatchRepoResult
                    {
                        Repo = repo,
                        Outcome = "failed",
                        Message = "could not acquire repo lock",
                    };
                }

                var r = _git.CreateBranch(repo.Root.FullName, new CreateBranchOptions
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
        return new BranchPlanResult { PlanId = plan.Id, DryRun = false, Results = results };
    }
}
