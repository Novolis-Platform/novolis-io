<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-io">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Watching

Single-file `FileSystemWatcher` helpers for editor-style reload loops: immediate (`SingleFileWatcher`) or **debounced** (`DebouncedFileWatcher`).

## Install

```bash
dotnet add package Novolis.IO.Watching
```

## Quick start

```csharp
using Novolis.IO.Watching;

using var watcher = new DebouncedFileWatcher(debounceMilliseconds: 250);
watcher.FileChanged += path => Reload(path);
watcher.Watch(@"D:\work\chapter.md");

// Later:
watcher.Stop();
```

Immediate (no debounce):

```csharp
using var raw = new SingleFileWatcher();
raw.FileChanged += path => Console.WriteLine($"changed: {path}");
raw.Watch(filePath);
```

## API

| Type | Role |
|------|------|
| `SingleFileWatcher` | Watches one file for change / rename / delete; raises `FileChanged` with the full path |
| `DebouncedFileWatcher` | Wraps `SingleFileWatcher`; coalesces bursts with `debounceMilliseconds` (default 300) |

### Notes

- `Watch` no-ops if the path is empty or the file does not exist yet.
- Dispose / `Stop` tears down the underlying `FileSystemWatcher`.
- Debounce uses a cancellable delay task; rapid saves only surface the last path after quiet.

## Dogfooding

```powershell
dotnet run --project ../novolis-dogfooding/apps/io/IoSmoke
```

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Recovery` | Persist mid-edit buffers to disk snapshots |
| `Novolis.IO.Workspace` | Root-scoped file access |

