using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Novolis.IO.Recovery;

/// <summary>Metadata for a recovery snapshot.</summary>
public sealed class RecoverySnapshotInfo
{
    /// <summary>Document key used when writing.</summary>
    public required string DocumentKey { get; init; }

    /// <summary>Path to the snapshot content file.</summary>
    public required string RecoveryPath { get; init; }

    /// <summary>Snapshot text.</summary>
    public required string Content { get; init; }

    /// <summary>UTC timestamp.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>SHA-256 hex of content.</summary>
    public string? ContentHash { get; init; }
}

/// <summary>Writes and trims content-hash recovery snapshots.</summary>
public sealed class ContentRecoveryStore
{
    /// <summary>Creates a store rooted at <paramref name="rootDirectory"/>.</summary>
    public ContentRecoveryStore(string rootDirectory, int maxSnapshotsPerDocument = 10)
    {
        RootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        MaxSnapshotsPerDocument = Math.Max(1, maxSnapshotsPerDocument);
    }

    /// <summary>Root directory for snapshots.</summary>
    public string RootDirectory { get; }

    /// <summary>Max snapshots retained per document key.</summary>
    public int MaxSnapshotsPerDocument { get; }

    /// <summary>Writes a snapshot for <paramref name="documentKey"/>.</summary>
    public void WriteSnapshot(string documentKey, string content)
    {
        var dir = Path.Combine(RootDirectory, SanitizeId(documentKey));
        Directory.CreateDirectory(dir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var mdPath = Path.Combine(dir, $"{stamp}.md");
        var metaPath = Path.Combine(dir, $"{stamp}.json");
        // Avoid same-ms overwrite when callers write back-to-back.
        if (File.Exists(mdPath))
        {
            stamp += "-" + Guid.NewGuid().ToString("N")[..8];
            mdPath = Path.Combine(dir, $"{stamp}.md");
            metaPath = Path.Combine(dir, $"{stamp}.json");
        }
        File.WriteAllText(mdPath, content);
        var meta = new
        {
            documentKey,
            timestampUtc = DateTime.UtcNow.ToString("o"),
            contentHash = Hash(content)
        };
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
        TrimOldSnapshots(dir);
    }

    /// <summary>Returns the latest snapshot for a document, if any.</summary>
    public RecoverySnapshotInfo? GetLatest(string documentKey)
    {
        var dir = Path.Combine(RootDirectory, SanitizeId(documentKey));
        if (!Directory.Exists(dir))
            return null;
        // Prefer write time over filename order: collision suffixes like stamp-guid sort
        // before stamp.md under ordinal compare and would pick the wrong "latest".
        var latest = Directory.GetFiles(dir, "*.md")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(static p => p, StringComparer.Ordinal)
            .FirstOrDefault();
        if (latest is null)
            return null;

        var ts = File.GetLastWriteTimeUtc(latest);
        string? hash = null;
        var metaPath = Path.ChangeExtension(latest, ".json");
        if (File.Exists(metaPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                if (doc.RootElement.TryGetProperty("timestampUtc", out var t)
                    && DateTime.TryParse(t.GetString(), out var parsed))
                    ts = parsed.ToUniversalTime();
                if (doc.RootElement.TryGetProperty("contentHash", out var h))
                    hash = h.GetString();
            }
            catch
            {
                /* ignore corrupt meta */
            }
        }

        return new RecoverySnapshotInfo
        {
            DocumentKey = documentKey,
            RecoveryPath = latest,
            Content = File.ReadAllText(latest),
            TimestampUtc = ts,
            ContentHash = hash
        };
    }

    /// <summary>Deletes all snapshots for a document key.</summary>
    public void Clear(string documentKey)
    {
        var dir = Path.Combine(RootDirectory, SanitizeId(documentKey));
        if (!Directory.Exists(dir))
            return;
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            /* ignore */
        }
    }

    void TrimOldSnapshots(string dir)
    {
        var files = Directory.GetFiles(dir, "*.md")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(static p => p, StringComparer.Ordinal)
            .ToList();
        foreach (var old in files.Skip(MaxSnapshotsPerDocument))
        {
            try
            {
                File.Delete(old);
                var meta = Path.ChangeExtension(old, ".json");
                if (File.Exists(meta))
                    File.Delete(meta);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    static string SanitizeId(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    static string Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
