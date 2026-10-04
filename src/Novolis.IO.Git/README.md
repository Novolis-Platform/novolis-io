<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-io/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-io/) · [Source](https://github.com/Novolis-Platform/novolis-io)
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Git

Process-based Git helper (requires `git` on `PATH`) for Studio and RepoStudio:

- Single-repo: status, checkpoint, passes, fetch/pull/push, branches, working tree, log, diff, stash
- Commit graph lane layout (`CommitGraphBuilder`) — Avalonia-free DTOs
- Forest: discover git children under a checkout root, status matrix, batch fetch/pull, branch-cut planner, fetch scheduler

## Install

```bash
dotnet add package Novolis.IO.Git
```

## Quick start

```csharp
using Novolis.IO.Git;
using Novolis.IO.Paths;

var git = new GitRepositoryService();
var status = git.GetStatus(repoRoot);
var graph = git.GetCommitGraph(repoRoot);

var root = CheckoutRoot.Resolve();
var forest = MultiGitRepositoryWorkspace.Discover(root);
var matrix = git.GetStatusMatrix(forest);
var batch = new GitRepositoryBatch(git);
await batch.FetchAsync(forest, new BatchOptions { WorkspaceRoot = root });
```

## Related

| Package | Role |
|---------|------|
| `Novolis.Avalonia.Git` | Avalonia chrome bound to these DTOs |
| `Novolis.IO.GitHub` | OAuth + sparse GitHub content mirror |
