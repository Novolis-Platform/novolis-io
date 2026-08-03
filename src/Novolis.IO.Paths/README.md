<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-io">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Paths

Walk parent directories to find a **workspace root** using caller-supplied markers (files/dirs) or a custom predicate. Used by dogfood apps and Studio hosts to locate repo roots without hard-coded paths.

## Install

```bash
dotnet add package Novolis.IO.Paths
```

## Quick start

```csharp
using Novolis.IO.Paths;

// All listed relative paths must exist under the root (file or directory).
if (RootFinder.TryFind(
        Environment.CurrentDirectory,
        ["nuget.config", "Directory.Packages.props"],
        out var root))
{
    Console.WriteLine(root);
}

// Or supply a custom predicate:
RootFinder.TryFind(startDir, dir => File.Exists(Path.Combine(dir.FullName, ".git", "HEAD")), out root);
```

## API

| Member | Role |
|--------|------|
| `RootFinder.TryFind(start, requiredRelativePaths, out root)` | Nearest ancestor where every marker exists |
| `RootFinder.TryFind(start, isRoot, out root)` | Nearest ancestor matching `Func<DirectoryInfo, bool>` |

On failure, `root` is set to `startDir` and the method returns `false`.

## Dogfooding

```powershell
dotnet run --project ../novolis-dogfooding/apps/io/IoSmoke
```

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Workspace` | Root-scoped file IO (published from novolis-storage) |

