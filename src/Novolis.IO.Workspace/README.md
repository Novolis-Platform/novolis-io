# Novolis.IO.Workspace

Root-scoped **file workspace** abstraction: `IFileProvider` for reads plus explicit write/delete/move helpers (Microsoft.Extensions.FileProviders has no write API on `IFileInfo`).

> **Packaging:** this project is `IsPackable=false` in **novolis-io**. The NuGet package is published from **novolis-storage**. Keep sources here in sync with that publish repo.

## Types (this repo)

| Type | Role |
|------|------|
| `IFileWorkspace` | Root path + provider + ensure/enumerate/read/write/delete/move |
| `PhysicalFileWorkspace` | Disk-backed implementation; also static on-disk probes (`FileExistsOnDisk`, …) |
| `FileWorkspaceKeys` | DI key constants (`Storage`, `JsonFileEvents`, `JsonFilesStore`) |

## Quick start

```csharp
using Novolis.IO.Workspace;

using var ws = new PhysicalFileWorkspace(rootPath);
ws.EnsureDirectoryExists(Path.Combine(ws.RootPath, "drafts"));
await ws.WriteAllTextAsync(Path.Combine(ws.RootPath, "drafts", "a.md"), "# hi");
var text = await ws.ReadAllTextAsync(Path.Combine(ws.RootPath, "drafts", "a.md"));
```

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Workspace.Testing` | In-memory `IFileWorkspace` for unit tests |
| `Novolis.IO.Paths` | Discover a workspace root before constructing `PhysicalFileWorkspace` |
| `Novolis.IO.Recovery` | Draft snapshots (often under a workspace subdirectory) |
