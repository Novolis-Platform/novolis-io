<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-io">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Processes

Bounded **process job queue** and **process-tree kill** helpers for hosted tools (build, git, converters). Jobs report status for UI binding; cancel kills the process tree on Windows via `taskkill /T /F`.

## Install

```bash
dotnet add package Novolis.IO.Processes
```

## Quick start

```csharp
using Novolis.IO.Processes;

var queue = new ProcessJobQueue { MaxParallel = 2 };
queue.Changed += () => RefreshUi(queue.Jobs);

var job = queue.Enqueue(new ProcessJobSpec
{
    FileName = "dotnet",
    Arguments = ["--version"],
    Title = "dotnet --version",
    WorkingDirectory = repoRoot,
});

// Poll or subscribe to Changed until job.Status is Succeeded / Failed / Cancelled
queue.Cancel(job); // optional
```

Kill an arbitrary PID tree:

```csharp
ProcessTree.Kill(pid);
```

## API

| Type | Role |
|------|------|
| `ProcessJobQueue` | Enqueue, cancel, `MaxParallel` (1–32), `Jobs`, `Changed` |
| `ProcessJobSpec` | `FileName`, `Arguments`, `WorkingDirectory`, `Title` |
| `ProcessJob` | `Id`, `Title`, `Status`, `Detail`, `ExitCode`, `CanCancel` |
| `ProcessJobStatus` | Queued / Running / Succeeded / Failed / Cancelled |
| `ProcessTree` | `Kill(pid)` — entire process tree |

Stdout/stderr are not captured into the job model (status + exit code + detail message only). Prefer redirecting yourself if you need logs.

## Dogfooding

```powershell
dotnet run --project ../novolis-dogfooding/apps/io/IoSmoke
```

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Git` | Specialized git process wrapper |
| `Novolis.IO.Mobile.Android` | ADB protocol driver (not a process queue) |

