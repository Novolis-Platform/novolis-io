using System.Text.Json;
using System.Text.Json.Serialization;
using Octokit;

namespace Novolis.IO.GitHub;

/// <summary>Result of Save/Commit/Push.</summary>
public sealed class MirrorPushResult
{
    /// <summary>Creates a push result.</summary>
    public MirrorPushResult(
        bool ok,
        string message,
        string? commitSha = null,
        int fileCount = 0,
        bool requiresReauthentication = false)
    {
        Ok = ok;
        Message = message;
        CommitSha = commitSha;
        FileCount = fileCount;
        RequiresReauthentication = requiresReauthentication;
    }

    /// <summary>Whether the push succeeded.</summary>
    public bool Ok { get; }

    /// <summary>Human-readable status.</summary>
    public string Message { get; }

    /// <summary>New commit SHA.</summary>
    public string? CommitSha { get; }

    /// <summary>Number of files included in the commit.</summary>
    public int FileCount { get; }

    /// <summary>Token was rejected — host should clear credentials and ask the user to sign in again.</summary>
    public bool RequiresReauthentication { get; }
}
