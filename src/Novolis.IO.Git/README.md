# Novolis.IO.Git

Thin Git process wrapper for status, add, commit, and checkpoint workflows.

## Install

```bash
dotnet add package Novolis.IO.Git
```

## Quick start

```csharp
using Novolis.IO.Git;

var git = new GitRepositoryService(new ProcessGitRunner());
var status = git.GetStatus(repoRoot);
```
