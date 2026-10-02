<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-io/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-io/) · [Source](https://github.com/Novolis-Platform/novolis-io)
<!-- novolis-pkg-brand:end -->

# Novolis.IO.Ndjson

Seekable newline-delimited JSON files for diagnostics, telemetry exports, and
other large append-only records.

`NdjsonFileReader` captures file identity without scanning to EOF. Reads extend
an in-memory sparse byte index only as far as the requested slice. Refresh
rechecks the previous boundary and scans only appended bytes when the file
continues unchanged. The reader uses shared, asynchronous file handles and
does not take an OS file lock.

`NdjsonFileWriter` appends one UTF-8 JSON value plus an LF and closes the file
after each write. It coordinates writers within one writer instance. Multiple
processes or pods must still assign one writer owner per file.

## Install

```bash
dotnet add package Novolis.IO.Ndjson
```

## Quick start

```csharp
using Novolis.IO.Ndjson;

var path = new FileInfo("events.ndjson");
using var writer = new NdjsonFileWriter(path);
writer.Append(new { Id = 1, Message = "ready" });

await using var document = await new NdjsonFileReader().OpenAsync(path);
var slice = await document.ReadAsync(skip: 0, take: 100);
foreach (var record in slice.Records)
    Console.WriteLine(record.Json?.GetRawText() ?? record.Raw);
```

An incomplete final physical line is excluded until a newline is present.
Malformed complete lines remain visible as records with `Error` and `Raw`.
