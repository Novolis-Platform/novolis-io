# Novolis.IO.Processes

Process job queue and process-tree helpers for hosted tools.

## Install

```bash
dotnet add package Novolis.IO.Processes
```

## Quick start

```csharp
using Novolis.IO.Processes;

var queue = new ProcessJobQueue();
queue.Enqueue(new ProcessJobSpec("git", ["status"], workingDirectory: repoRoot));
```
