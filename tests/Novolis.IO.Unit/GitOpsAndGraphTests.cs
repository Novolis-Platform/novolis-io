using Novolis.IO.Git;

namespace Novolis.IO.Unit;

public sealed class GitOpsAndGraphTests
{
    [Test]
    public async Task Fetch_and_pull_ff_only()
    {
        var flex = new FlexibleGitRunner();
        flex.When(a => a is ["rev-parse", "--git-dir"], 0, ".git\n");
        flex.When(a => a is ["fetch", "origin", "--prune"], 0, "");
        flex.When(a => a is ["pull", "origin", "--ff-only"], 0, "Already up to date.\n");

        var git = new GitRepositoryService(flex);
        var fetch = git.Fetch("/repo");
        await Assert.That(fetch.Ok).IsTrue();
        var pull = git.PullFfOnly("/repo");
        await Assert.That(pull.Ok).IsTrue();
    }

    [Test]
    public async Task Working_tree_groups_porcelain()
    {
        var flex = new FlexibleGitRunner();
        flex.When(a => a is ["rev-parse", "--git-dir"], 0, ".git\n");
        flex.When(a => a is ["status", "--porcelain"], 0, "M  staged.cs\n M unstaged.cs\n?? untracked.cs\n");

        var git = new GitRepositoryService(flex);
        var wt = git.GetWorkingTree("/repo");
        await Assert.That(wt.Staged.Count).IsEqualTo(1);
        await Assert.That(wt.Unstaged.Count).IsEqualTo(1);
        await Assert.That(wt.Untracked.Count).IsEqualTo(1);
    }

    [Test]
    public async Task List_stashes_parses_entries()
    {
        var flex = new FlexibleGitRunner();
        flex.When(a => a is ["rev-parse", "--git-dir"], 0, ".git\n");
        flex.When(a => a.Length >= 2 && a[0] == "stash" && a[1] == "list", 0,
            "stash@{0}\u001fWIP\u001f2026-01-01T00:00:00Z\u001fabc\nstash@{1}\u001fold\u001f2026-01-02T00:00:00Z\u001fdef\n");

        var git = new GitRepositoryService(flex);
        var stashes = git.ListStashes("/repo");
        await Assert.That(stashes.Count).IsEqualTo(2);
        await Assert.That(stashes[0].Index).IsEqualTo(0);
        await Assert.That(stashes[0].Message).IsEqualTo("WIP");
    }

    [Test]
    public async Task CreateBranch_checkout_B()
    {
        var flex = new FlexibleGitRunner();
        flex.When(a => a is ["rev-parse", "--git-dir"], 0, ".git\n");
        flex.When(a => a is ["checkout", "-B", "feat/x", "main"], 0, "");

        var git = new GitRepositoryService(flex);
        var r = git.CreateBranch("/repo", new CreateBranchOptions { Name = "feat/x", BaseRef = "main" });
        await Assert.That(r.Ok).IsTrue();
    }

    [Test]
    public async Task DiffParser_reads_hunks()
    {
        var text = string.Join('\n',
            "diff --git a/a.cs b/a.cs",
            "--- a/a.cs",
            "+++ b/a.cs",
            "@@ -1,2 +1,3 @@",
            " keep",
            "-old",
            "+new",
            "+extra");
        var doc = DiffParser.Parse(text);
        await Assert.That(doc.Files.Count).IsEqualTo(1);
        await Assert.That(doc.Files[0].Path).IsEqualTo("a.cs");
        await Assert.That(doc.Files[0].Hunks.Count).IsEqualTo(1);
        await Assert.That(doc.Files[0].Hunks[0].Lines.Count).IsEqualTo(4);
    }

    [Test]
    public async Task CommitGraphBuilder_assigns_lanes_and_merge_edge()
    {
        var commits = new List<CommitInfo>
        {
            new()
            {
                Sha = "c3", ShortSha = "c3", Subject = "merge",
                Parents = ["c2", "b2"],
            },
            new()
            {
                Sha = "b2", ShortSha = "b2", Subject = "feat",
                Parents = ["c1"],
            },
            new()
            {
                Sha = "c2", ShortSha = "c2", Subject = "main2",
                Parents = ["c1"],
            },
            new()
            {
                Sha = "c1", ShortSha = "c1", Subject = "base",
                Parents = [],
            },
        };

        var graph = CommitGraphBuilder.Build(commits, [new TipRef { Name = "main", Kind = GitRefKind.Branch, Sha = "c3" }]);
        await Assert.That(graph.Nodes.Count).IsEqualTo(4);
        await Assert.That(graph.Edges.Any(e => e.Kind == CommitEdgeKind.Merge)).IsTrue();
        await Assert.That(graph.Lanes.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GitWorkspace_discover_and_filter_names()
    {
        var root = Directory.CreateTempSubdirectory("novolis-ws-");
        try
        {
            var a = Path.Combine(root.FullName, "novolis-alpha");
            var b = Path.Combine(root.FullName, "novolis-beta");
            Directory.CreateDirectory(Path.Combine(a, ".git"));
            Directory.CreateDirectory(b); // no git
            File.WriteAllText(Path.Combine(root.FullName, "Novolis.Platform.slnx"), "");

            var found = GitWorkspace.Discover(root.FullName);
            await Assert.That(found.Count).IsEqualTo(2);
            await Assert.That(found.Count(r => r.IsGit)).IsEqualTo(1);

            var selected = GitWorkspace.SelectByNames(found, new RepoFilter { Include = ["alpha"] });
            await Assert.That(selected.Count).IsEqualTo(1);
            await Assert.That(selected[0].Name).IsEqualTo("novolis-alpha");

            var resolved = GitWorkspace.ResolveRoot(root.FullName);
            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(root.FullName));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public async Task BranchCutPlanner_dry_run_skips_dirty()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-cut-");
        try
        {
            var repoPath = Path.Combine(temp.FullName, "novolis-x");
            Directory.CreateDirectory(Path.Combine(repoPath, ".git"));

            var flex = new FlexibleGitRunner();
            flex.When(a => a is ["rev-parse", "--git-dir"], 0, ".git\n");
            flex.When(a => a is ["rev-parse", "--abbrev-ref", "HEAD"], 0, "main\n");
            flex.When(a => a is ["status", "--porcelain"], 0, " M dirty.cs\n");
            flex.When(a => a[0] == "rev-parse" && a[1] == "--abbrev-ref" && a[2] == "--symbolic-full-name", 1, "", "fatal\n");
            flex.When(a => a[0] == "log", 0, "2026-01-01T00:00:00Z|abc|msg\n");

            // GetStatus needs more stubs
            flex.When(a => a.Length >= 1 && a[0] == "rev-list", 0, "0\t0\n");

            var git = new GitRepositoryService(flex);
            // Simpler: use status that returns dirty via a dedicated path - GetStatus uses many calls
            // Rebuild flex carefully for GetStatus
            var flex2 = new FlexibleGitRunner();
            flex2.When(a => a is ["rev-parse", "--git-dir"], 0, ".git\n");
            flex2.When(a => a is ["rev-parse", "--abbrev-ref", "HEAD"], 0, "main\n");
            flex2.When(a => a is ["status", "--porcelain"], 0, " M dirty.cs\n");
            flex2.When(a => a.Length >= 3 && a[0] == "rev-parse" && a[1] == "--abbrev-ref", 1, "", "fatal\n");
            flex2.When(a => a[0] == "log", 0, "2026-01-01T00:00:00Z|abc|msg\n");

            var planner = new BranchCutPlanner(new GitRepositoryService(flex2));
            var plan = planner.Plan(temp.FullName, "feat/x",
            [
                new RepoEntry { Name = "novolis-x", Path = repoPath, IsGit = true }
            ]);
            await Assert.That(plan.Steps[0].BlockReason).IsEqualTo("dirty worktree");

            var applied = await planner.ApplyAsync(plan, dryRun: true);
            await Assert.That(applied.Results[0].Outcome).IsEqualTo("skipped");
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
