# Novolis.IO.Recovery

Content-hash **draft recovery** snapshots for unsaved or mid-edit document buffers. Writes `.md` + `.json` meta under a configurable root and trims to a max count per document key.

## Install

```bash
dotnet add package Novolis.IO.Recovery
```

## Quick start

```csharp
using Novolis.IO.Recovery;

var store = new ContentRecoveryStore(recoveryRoot, maxSnapshotsPerDocument: 10);
store.WriteSnapshot("chapter-1", "# Draft…");

var latest = store.GetLatest("chapter-1");
if (latest is not null)
    Console.WriteLine($"{latest.TimestampUtc:u} hash={latest.ContentHash}");

store.Clear("chapter-1");
```

## API

| Type | Role |
|------|------|
| `ContentRecoveryStore` | Write / get latest / clear snapshots |
| `RecoverySnapshotInfo` | Document key, path, content, UTC time, SHA-256 hex |

### Layout

```text
{root}/{sanitizedDocumentKey}/
  20260731T120000Z.md
  20260731T120000Z.json   # documentKey, timestampUtc, contentHash
```

Older `.md` files (and paired `.json`) beyond `MaxSnapshotsPerDocument` are deleted on write.

## Dogfooding

```powershell
dotnet run --project ../novolis-dogfooding/apps/io/IoSmoke
```

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Watching` | Detect file changes that may trigger reload/recover UI |
| `Novolis.IO.Workspace` | Root-scoped file IO for apps |
