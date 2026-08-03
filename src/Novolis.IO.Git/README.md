<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-io">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Git

Thin **process-based** Git helper for Studio-style status, checkpoint commits, pass tracking, and revision tags. Shells out to `git` on PATH via `IGitProcessRunner` (same pattern as other Novolis “driver” packages).

## Install

```bash
dotnet add package Novolis.IO.Git
```

Requires `git` on `PATH`.

## Quick start

```csharp
using Novolis.IO.Git;

var git = new GitRepositoryService(); // uses ProcessGitRunner
var status = git.GetStatus(repoRoot);
Console.WriteLine($"{status.Branch} dirty={status.Dirty} ahead={status.Ahead}");

var checkpoint = git.Checkpoint(repoRoot, "WIP: save point", new CheckpointOptions
{
    Push = false,
    // Pathspecs = ["src/..."], // optional; default stages -A
});
```

## API

| Type | Role |
|------|------|
| `GitRepositoryService` | Status, checkpoint, pass start/finish, revision tags |
| `GitStatus` | Branch, upstream ahead/behind, dirty files, last commit, active pass |
| `CheckpointOptions` | Optional pathspecs + push-after-commit |
| `GitOperationResult` | Ok / command / message (+ optional data) |
| `IGitProcessRunner` / `ProcessGitRunner` | Injectable `git` process runner (tests use fakes) |

### Operations

```csharp
git.GetStatus(repoRoot);
git.Checkpoint(repoRoot, message, options);
git.PassStart(repoRoot);              // records active pass under .novolis/git-passes.json
git.PassFinish(repoRoot);
git.CreateRevisionTag(repoRoot, "v1.2.3");
```

Pass metadata defaults to `.novolis/git-passes.json` (override via ctor).

## Dogfooding

```powershell
dotnet run --project ../novolis-dogfooding/apps/io/IoSmoke
```

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.GitHub` | OAuth + sparse GitHub `content/` mirror (no local `git`) |
| `Novolis.IO.Processes` | Generic process job queue |

