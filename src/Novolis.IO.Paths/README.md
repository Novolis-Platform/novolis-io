# Novolis.IO.Paths

Workspace root discovery helpers with caller-supplied markers.

## Install

```bash
dotnet add package Novolis.IO.Paths
```

## Quick start

```csharp
using Novolis.IO.Paths;

if (RootFinder.TryFind(Environment.CurrentDirectory, [".git", "global.json"], out var root))
{
    Console.WriteLine(root);
}
```
