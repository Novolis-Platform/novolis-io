# Novolis.IO.Workspace.Abstractions

The fundamental typed directory-root contract for Novolis workspace-shaped resources.

```csharp
using Novolis.IO.Workspace;

IWorkspace workspace = GetWorkspace();
var rootPath = workspace.Root.FullName;
```

`IWorkspace` owns only directory identity. `IFileWorkspace` adds explicit file operations in
`Novolis.IO.Workspace`; structured project workspaces, solutions, Git repositories, and domain
content roots compose this abstraction in their owning packages.
