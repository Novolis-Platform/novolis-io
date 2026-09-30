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
