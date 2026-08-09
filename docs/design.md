# Design

IO helpers: Git, watching, recovery, processes, and path utilities.

## Goals

- Keep public APIs documented and packable as `Novolis.*` on GitHub Packages.
- Respect the closed platform spine and orthogonal islands ([library-boundaries](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/library-boundaries.md)).

## Non-goals

- Local NuGet folder feeds or cross-repo `ProjectReference` in committed `.csproj` files.
- Pulling Avalonia into non-`Novolis.Avalonia.*` libraries.

## Topics

- `dotnet`
- `io`
- `novolis`
