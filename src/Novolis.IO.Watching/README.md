# Novolis.IO.Watching

Single-file and debounced file watchers for editor-style reload loops.

## Install

```bash
dotnet add package Novolis.IO.Watching
```

## Quick start

```csharp
using Novolis.IO.Watching;

using var watcher = new DebouncedFileWatcher(path, TimeSpan.FromMilliseconds(250), () => Reload());
```
