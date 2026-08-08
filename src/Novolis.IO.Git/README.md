<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-io">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Git

Process-based Git helper (requires `git` on `PATH`) for Studio and RepoStudio:

- Single-repo: status, checkpoint, passes, fetch/pull/push, branches, working tree, log, diff, stash
- Commit graph lane layout (`CommitGraphBuilder`) — Avalonia-free DTOs
- Workspace: discover `novolis-*`, status matrix, batch fetch/pull, branch-cut planner, fetch scheduler

## Install

```bash
dotnet add package Novolis.IO.Git
```

## Quick start

```csharp
using Novolis.IO.Git;

var git = new GitRepositoryService();
var status = git.GetStatus(repoRoot);
var graph = git.GetCommitGraph(repoRoot);

var root = GitWorkspace.ResolveRoot();
var matrix = GitWorkspace.GetStatusMatrix(root, git);
var batch = new GitWorkspaceBatch(git);
await batch.FetchAsync(GitWorkspace.SelectByNames(GitWorkspace.Discover(root), null),
    new BatchOptions { WorkspaceRoot = root });
```

## Related

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Git` | Avalonia chrome bound to these DTOs |
| `Novolis.IO.GitHub` | OAuth + sparse GitHub content mirror |
