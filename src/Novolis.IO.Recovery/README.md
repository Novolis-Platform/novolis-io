# Novolis.IO.Recovery

Content recovery snapshots for unsaved or mid-edit document buffers.

## Install

```bash
dotnet add package Novolis.IO.Recovery
```

## Quick start

```csharp
using Novolis.IO.Recovery;

var store = new ContentRecoveryStore(recoveryRoot);
store.WriteSnapshot(documentId, content);
```
