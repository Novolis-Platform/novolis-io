# Novolis.IO.Workspace.Testing

In-memory **`IFileWorkspace`** for unit tests — no disk IO. Implements the same surface as `PhysicalFileWorkspace` using concurrent dictionaries.

## Install

```powershell
dotnet add package Novolis.IO.Workspace.Testing
```

## Quick start

```csharp
using Novolis.IO.Workspace.Testing;

using var ws = new InMemoryFileWorkspace(@"C:\virtual\root");
ws.WriteAllText(Path.Combine(ws.RootPath, "a.txt"), "hello");
Assert.True(ws.FileExists(Path.Combine(ws.RootPath, "a.txt")));
```

## API

| Type | Role |
|------|------|
| `InMemoryFileWorkspace` | Volatile files/directories + `IFileProvider` |

Paths are normalized; directory existence is inferred from known dirs and file key prefixes.

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Workspace` | Production `IFileWorkspace` / `PhysicalFileWorkspace` |
