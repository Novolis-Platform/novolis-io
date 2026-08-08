namespace Novolis.IO.Git;

public sealed partial class GitRepositoryService
{
    /// <summary>Fetches from remote (no merge).</summary>
    public GitOperationResult Fetch(string repoRoot, string remote = "origin")
    {
        EnsureGitRepo(repoRoot);
        var r = Run(repoRoot, "fetch", remote, "--prune");
        return r.ExitCode == 0
            ? GitOperationResult.Success("fetch", $"Fetched {remote}.")
            : GitOperationResult.Fail("fetch", r.StdErr.Trim(), r);
    }

    /// <summary>Pulls with ff-only by default.</summary>
    public GitOperationResult PullFfOnly(string repoRoot, PullOptions? options = null)
    {
        EnsureGitRepo(repoRoot);
        options ??= new PullOptions();
        var args = new List<string> { "pull", options.Remote };
        if (options.FfOnly)
            args.Add("--ff-only");
        var r = Run(repoRoot, args.ToArray());
        return r.ExitCode == 0
            ? GitOperationResult.Success("pull", "Pulled.")
            : GitOperationResult.Fail("pull", r.StdErr.Trim(), r);
    }

    /// <summary>Pushes current branch (force only when options say so).</summary>
    public GitOperationResult Push(string repoRoot, PushOptions? options = null)
    {
        EnsureGitRepo(repoRoot);
        options ??= new PushOptions();
        var args = new List<string> { "push" };
        if (options.Force)
            args.Add("--force-with-lease");
        args.Add(options.Remote);
        if (options.SetUpstream)
        {
            args.Add("-u");
            var branch = RunText(repoRoot, "rev-parse", "--abbrev-ref", "HEAD").Trim();
            args.Add(branch);
        }

        var r = Run(repoRoot, args.ToArray());
        return r.ExitCode == 0
            ? GitOperationResult.Success("push", options.Force ? "Force-with-lease pushed." : "Pushed.")
            : GitOperationResult.Fail("push", r.StdErr.Trim(), r);
    }

    /// <summary>Lists local branches, remotes, and tags.</summary>
    public BranchList ListBranches(string repoRoot)
    {
        EnsureGitRepo(repoRoot);
        var current = RunText(repoRoot, "rev-parse", "--abbrev-ref", "HEAD").Trim();
        var local = ParseRefLines(RunText(repoRoot, "for-each-ref", "--format=%(refname:short)|%(objectname:short)", "refs/heads"), GitRefKind.Branch);
        var remote = ParseRefLines(RunText(repoRoot, "for-each-ref", "--format=%(refname:short)|%(objectname:short)", "refs/remotes"), GitRefKind.Remote);
        var tags = ParseRefLines(RunText(repoRoot, "for-each-ref", "--format=%(refname:short)|%(objectname:short)", "refs/tags"), GitRefKind.Tag);
        return new BranchList { Current = current, Local = local, Remote = remote, Tags = tags };
    }

    /// <summary>Checks out an existing branch or ref.</summary>
    public GitOperationResult Checkout(string repoRoot, string refName)
    {
        EnsureGitRepo(repoRoot);
        if (string.IsNullOrWhiteSpace(refName))
            return GitOperationResult.Fail("checkout", "Ref is required.");
        var r = Run(repoRoot, "checkout", refName);
        return r.ExitCode == 0
            ? GitOperationResult.Success("checkout", $"Checked out {refName}.")
            : GitOperationResult.Fail("checkout", r.StdErr.Trim(), r);
    }

    /// <summary>Creates a branch (optionally from base) and checks it out by default.</summary>
    public GitOperationResult CreateBranch(string repoRoot, CreateBranchOptions options)
    {
        EnsureGitRepo(repoRoot);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
            return GitOperationResult.Fail("branch", "Branch name is required.");

        var baseRef = string.IsNullOrWhiteSpace(options.BaseRef) ? "HEAD" : options.BaseRef!;
        if (options.Checkout)
        {
            var r = Run(repoRoot, "checkout", "-B", options.Name, baseRef);
            return r.ExitCode == 0
                ? GitOperationResult.Success("branch", $"Created and checked out {options.Name}.")
                : GitOperationResult.Fail("branch", r.StdErr.Trim(), r);
        }

        var create = Run(repoRoot, "branch", options.Name, baseRef);
        return create.ExitCode == 0
            ? GitOperationResult.Success("branch", $"Created {options.Name}.")
            : GitOperationResult.Fail("branch", create.StdErr.Trim(), create);
    }

    /// <summary>Parses porcelain into staged / unstaged / untracked groups.</summary>
    public WorkingTreeStatus GetWorkingTree(string repoRoot)
    {
        EnsureGitRepo(repoRoot);
        var porcelain = RunText(repoRoot, "status", "--porcelain").Trim();
        var staged = new List<WorkingTreeEntry>();
        var unstaged = new List<WorkingTreeEntry>();
        var untracked = new List<WorkingTreeEntry>();

        foreach (var line in porcelain.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3)
                continue;
            var code = line[..2];
            var path = line[3..].Trim();
            if (path.Contains(" -> ", StringComparison.Ordinal))
                path = path[(path.IndexOf(" -> ", StringComparison.Ordinal) + 4)..];

            if (code is "??" or "!!")
            {
                untracked.Add(new WorkingTreeEntry { Path = path, Group = WorkingTreeGroup.Untracked, StatusCode = code });
                continue;
            }

            if (code[0] is not ' ' and not '?')
                staged.Add(new WorkingTreeEntry { Path = path, Group = WorkingTreeGroup.Staged, StatusCode = code });
            if (code[1] is not ' ' and not '?')
                unstaged.Add(new WorkingTreeEntry { Path = path, Group = WorkingTreeGroup.Unstaged, StatusCode = code });
        }

        return new WorkingTreeStatus { Staged = staged, Unstaged = unstaged, Untracked = untracked };
    }

    /// <summary>Reads a commit log.</summary>
    public IReadOnlyList<CommitInfo> GetCommitLog(string repoRoot, CommitLogOptions? options = null)
    {
        EnsureGitRepo(repoRoot);
        options ??= new CommitLogOptions();
        var args = new List<string>
        {
            "log",
            $"-n{Math.Clamp(options.MaxCount, 1, 5000)}",
            "--format=%H%x1f%h%x1f%s%x1f%b%x1f%an%x1f%ae%x1f%aI%x1f%P%x1e",
        };
        if (options.FirstParent)
            args.Add("--first-parent");

        var raw = RunText(repoRoot, args.ToArray());
        var list = new List<CommitInfo>();
        foreach (var record in raw.Split('\u001e', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = record.Split('\u001f');
            if (parts.Length < 8)
                continue;
            var parents = string.IsNullOrWhiteSpace(parts[7])
                ? Array.Empty<string>()
                : parts[7].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            list.Add(new CommitInfo
            {
                Sha = parts[0].Trim(),
                ShortSha = parts[1].Trim(),
                Subject = parts[2].Trim(),
                Body = string.IsNullOrWhiteSpace(parts[3]) ? null : parts[3].Trim(),
                AuthorName = parts[4].Trim(),
                AuthorEmail = parts[5].Trim(),
                AuthorAt = parts[6].Trim(),
                Parents = parents,
            });
        }

        return list;
    }

    /// <summary>Commit detail with name-status summary.</summary>
    public CommitDetail GetCommitDetail(string repoRoot, string sha)
    {
        EnsureGitRepo(repoRoot);
        var show = RunText(repoRoot, "log", "-1", "--format=%H%x1f%h%x1f%s%x1f%b%x1f%an%x1f%ae%x1f%aI%x1f%P", sha).Trim();
        var parts = show.Split('\u001f');
        if (parts.Length < 8)
            throw new InvalidOperationException($"Commit not found: {sha}");
        var parents = string.IsNullOrWhiteSpace(parts[7])
            ? Array.Empty<string>()
            : parts[7].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var commit = new CommitInfo
        {
            Sha = parts[0].Trim(),
            ShortSha = parts[1].Trim(),
            Subject = parts[2].Trim(),
            Body = string.IsNullOrWhiteSpace(parts[3]) ? null : parts[3].Trim(),
            AuthorName = parts[4].Trim(),
            AuthorEmail = parts[5].Trim(),
            AuthorAt = parts[6].Trim(),
            Parents = parents,
        };

        var nameStatus = RunText(repoRoot, "diff-tree", "--no-commit-id", "--name-status", "-r", sha);
        var paths = new List<string>();
        var added = 0;
        var deleted = 0;
        var modified = 0;
        foreach (var line in nameStatus.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0)
                continue;
            var status = line[..tab];
            var path = line[(tab + 1)..];
            paths.Add(path);
            if (status.StartsWith('A')) added++;
            else if (status.StartsWith('D')) deleted++;
            else modified++;
        }

        return new CommitDetail
        {
            Commit = commit,
            FilesAdded = added,
            FilesDeleted = deleted,
            FilesModified = modified,
            Paths = paths,
        };
    }

    /// <summary>Unified diff for a commit (vs first parent) or working tree when sha is null.</summary>
    public DiffDocument GetDiff(string repoRoot, string? sha = null)
    {
        EnsureGitRepo(repoRoot);
        GitProcessResult r;
        if (string.IsNullOrWhiteSpace(sha))
            r = Run(repoRoot, "diff", "HEAD");
        else
            r = Run(repoRoot, "show", "--format=", "--unified=3", sha);

        if (r.ExitCode != 0 && string.IsNullOrWhiteSpace(r.StdOut))
            return new DiffDocument();

        return DiffParser.Parse(r.StdOut);
    }

    /// <summary>Lists stashes.</summary>
    public IReadOnlyList<StashEntry> ListStashes(string repoRoot)
    {
        EnsureGitRepo(repoRoot);
        var raw = RunText(repoRoot, "stash", "list", "--format=%gd%x1f%gs%x1f%ci%x1f%H");
        var list = new List<StashEntry>();
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\u001f');
            if (parts.Length < 2)
                continue;
            var gd = parts[0].Trim(); // stash@{n}
            var idx = 0;
            var open = gd.IndexOf('{');
            var close = gd.IndexOf('}');
            if (open >= 0 && close > open)
                _ = int.TryParse(gd[(open + 1)..close], out idx);
            list.Add(new StashEntry
            {
                Index = idx,
                Message = parts[1].Trim(),
                At = parts.Length > 2 ? parts[2].Trim() : null,
                Sha = parts.Length > 3 ? parts[3].Trim() : null,
            });
        }

        return list;
    }

    /// <summary>Creates a stash.</summary>
    public GitOperationResult StashPush(string repoRoot, string? message = null)
    {
        EnsureGitRepo(repoRoot);
        var args = new List<string> { "stash", "push", "-u" };
        if (!string.IsNullOrWhiteSpace(message))
        {
            args.Add("-m");
            args.Add(message);
        }

        var r = Run(repoRoot, args.ToArray());
        return r.ExitCode == 0
            ? GitOperationResult.Success("stash push", "Stashed.")
            : GitOperationResult.Fail("stash push", r.StdErr.Trim(), r);
    }

    /// <summary>Applies a stash without dropping.</summary>
    public GitOperationResult StashApply(string repoRoot, int index = 0)
    {
        EnsureGitRepo(repoRoot);
        var r = Run(repoRoot, "stash", "apply", $"stash@{{{index}}}");
        return r.ExitCode == 0
            ? GitOperationResult.Success("stash apply", $"Applied stash@{{{index}}}.")
            : GitOperationResult.Fail("stash apply", r.StdErr.Trim(), r);
    }

    /// <summary>Pops a stash.</summary>
    public GitOperationResult StashPop(string repoRoot, int index = 0)
    {
        EnsureGitRepo(repoRoot);
        var r = Run(repoRoot, "stash", "pop", $"stash@{{{index}}}");
        return r.ExitCode == 0
            ? GitOperationResult.Success("stash pop", $"Popped stash@{{{index}}}.")
            : GitOperationResult.Fail("stash pop", r.StdErr.Trim(), r);
    }

    /// <summary>Drops a stash.</summary>
    public GitOperationResult StashDrop(string repoRoot, int index = 0)
    {
        EnsureGitRepo(repoRoot);
        var r = Run(repoRoot, "stash", "drop", $"stash@{{{index}}}");
        return r.ExitCode == 0
            ? GitOperationResult.Success("stash drop", $"Dropped stash@{{{index}}}.")
            : GitOperationResult.Fail("stash drop", r.StdErr.Trim(), r);
    }

    /// <summary>Builds a commit graph model for UI / JSON.</summary>
    public CommitGraphModel GetCommitGraph(string repoRoot, CommitGraphOptions? options = null)
    {
        options ??= new CommitGraphOptions();
        var commits = GetCommitLog(repoRoot, new CommitLogOptions
        {
            MaxCount = options.MaxCount,
            FirstParent = options.FirstParent,
        });
        var branches = ListBranches(repoRoot);
        var tips = new List<TipRef>();
        tips.AddRange(branches.Local);
        tips.AddRange(branches.Tags);
        return CommitGraphBuilder.Build(commits, tips, options);
    }

    static IReadOnlyList<TipRef> ParseRefLines(string raw, GitRefKind kind)
    {
        var list = new List<TipRef>();
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', 2);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                continue;
            list.Add(new TipRef
            {
                Name = parts[0].Trim(),
                Kind = kind,
                Sha = parts.Length > 1 ? parts[1].Trim() : null,
            });
        }

        return list;
    }
}
