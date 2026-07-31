<!-- novolis-package-index:start -->
> **GitHub Packages shows this repository README on every package page** (upstream limitation).

## Published packages

| Package | Install | Notes |
|---------|---------|-------|
| `Novolis.IO.Git` | `dotnet add package Novolis.IO.Git` | Process-based git status / checkpoint / passes |
| `Novolis.IO.GitHub` | `dotnet add package Novolis.IO.GitHub` | OAuth device flow + sparse `content/` mirror |
| `Novolis.IO.Watching` | `dotnet add package Novolis.IO.Watching` | Single-file + debounced watchers |
| `Novolis.IO.Recovery` | `dotnet add package Novolis.IO.Recovery` | Content-hash draft snapshots |
| `Novolis.IO.Processes` | `dotnet add package Novolis.IO.Processes` | Process job queue + tree kill |
| `Novolis.IO.Paths` | `dotnet add package Novolis.IO.Paths` | Workspace root discovery |
| `Novolis.IO.Mobile.Android` | `dotnet add package Novolis.IO.Mobile.Android` | Host ADB protocol: devices, stats, APK install |

`Novolis.IO.Workspace` / `.Testing` are published from **novolis-storage** (sources also live under `src/` here).

<!-- novolis-package-index:end -->

# novolis-io

Small **I/O and tooling** libraries for Novolis apps: git/GitHub, paths, watching, recovery, process jobs, and host-side Android ADB.

## Package docs

| Package | README |
|---------|--------|
| Git | [src/Novolis.IO.Git/README.md](src/Novolis.IO.Git/README.md) |
| GitHub | [src/Novolis.IO.GitHub/README.md](src/Novolis.IO.GitHub/README.md) |
| Paths | [src/Novolis.IO.Paths/README.md](src/Novolis.IO.Paths/README.md) |
| Watching | [src/Novolis.IO.Watching/README.md](src/Novolis.IO.Watching/README.md) |
| Recovery | [src/Novolis.IO.Recovery/README.md](src/Novolis.IO.Recovery/README.md) |
| Processes | [src/Novolis.IO.Processes/README.md](src/Novolis.IO.Processes/README.md) |
| Mobile.Android | [src/Novolis.IO.Mobile.Android/README.md](src/Novolis.IO.Mobile.Android/README.md) |
| Workspace | [src/Novolis.IO.Workspace/README.md](src/Novolis.IO.Workspace/README.md) |
| Workspace.Testing | [src/Novolis.IO.Workspace.Testing/README.md](src/Novolis.IO.Workspace.Testing/README.md) |

## Dogfooding

| App | Repo | Covers |
|-----|------|--------|
| `IoSmoke` | novolis-dogfooding | Paths, Recovery, Watching, Processes, Git |
| `AdbLab` | novolis-dogfooding | Mobile.Android (protocol, stats, install helpers) |
