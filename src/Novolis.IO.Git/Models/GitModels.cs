namespace Novolis.IO.Git;

/// <summary>Options for checkpoint commits.</summary>
public sealed class CheckpointOptions
{
    /// <summary>When set, only these pathspecs are staged (instead of <c>-A</c>).</summary>
    public IReadOnlyList<string>? Pathspecs { get; init; }

    /// <summary>Whether to push after a successful commit.</summary>
    public bool Push { get; init; } = true;
}

/// <summary>Compact git status snapshot.</summary>
public sealed class GitStatus
{
    /// <summary>Current branch name.</summary>
    public required string Branch { get; init; }

    /// <summary>Upstream branch, if any.</summary>
    public string? Upstream { get; init; }

    /// <summary>Commits ahead of upstream.</summary>
    public int Ahead { get; init; }

    /// <summary>Commits behind upstream.</summary>
    public int Behind { get; init; }

    /// <summary>Whether the work tree is dirty.</summary>
    public bool Dirty { get; init; }

    /// <summary>Porcelain dirty file lines.</summary>
    public IReadOnlyList<string> DirtyFiles { get; init; } = [];

    /// <summary>Active pass id from the pass store, if any.</summary>
    public string? ActivePass { get; init; }

    /// <summary>Last commit ISO timestamp.</summary>
    public string? LastCommitAt { get; init; }

    /// <summary>Last commit short sha.</summary>
    public string? LastCommitSha { get; init; }

    /// <summary>Last commit subject.</summary>
    public string? LastCommitMessage { get; init; }
}

/// <summary>Generic operation result.</summary>
public sealed class GitOperationResult
{
    /// <summary>Creates a result.</summary>
    public GitOperationResult(bool ok, string command, string message, object? data = null)
    {
        Ok = ok;
        Command = command;
        Message = message;
        Data = data;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Ok { get; }

    /// <summary>Logical command name.</summary>
    public string Command { get; }

    /// <summary>Human-readable message.</summary>
    public string Message { get; }

    /// <summary>Optional structured payload.</summary>
    public object? Data { get; }

    /// <summary>Success factory.</summary>
    public static GitOperationResult Success(string command, string message, object? data = null) =>
        new(true, command, message, data);

    /// <summary>Failure factory.</summary>
    public static GitOperationResult Fail(string command, string message, object? data = null) =>
        new(false, command, message, data);
}

/// <summary>Kind of git tip ref.</summary>
public enum GitRefKind
{
    /// <summary>Local branch.</summary>
    Branch,

    /// <summary>Remote-tracking branch.</summary>
    Remote,

    /// <summary>Tag.</summary>
    Tag,
}

/// <summary>A named tip (branch / remote / tag).</summary>
public sealed class TipRef
{
    /// <summary>Ref short name.</summary>
    public required string Name { get; init; }

    /// <summary>Ref kind.</summary>
    public required GitRefKind Kind { get; init; }

    /// <summary>Target commit sha (full or abbreviated).</summary>
    public string? Sha { get; init; }

    /// <summary>Upstream ahead count when known.</summary>
    public int? Ahead { get; init; }

    /// <summary>Upstream behind count when known.</summary>
    public int? Behind { get; init; }
}

/// <summary>Local / remote / tag listing.</summary>
public sealed class BranchList
{
    /// <summary>Current branch name (or HEAD).</summary>
    public required string Current { get; init; }

    /// <summary>Local branches.</summary>
    public IReadOnlyList<TipRef> Local { get; init; } = [];

    /// <summary>Remote-tracking branches.</summary>
    public IReadOnlyList<TipRef> Remote { get; init; } = [];

    /// <summary>Tags.</summary>
    public IReadOnlyList<TipRef> Tags { get; init; } = [];
}

/// <summary>Working tree path group.</summary>
public enum WorkingTreeGroup
{
    /// <summary>Staged for commit.</summary>
    Staged,

    /// <summary>Modified but unstaged.</summary>
    Unstaged,

    /// <summary>Untracked.</summary>
    Untracked,
}

/// <summary>One working-tree path entry.</summary>
public sealed class WorkingTreeEntry
{
    /// <summary>Repository-relative path.</summary>
    public required string Path { get; init; }

    /// <summary>Group.</summary>
    public required WorkingTreeGroup Group { get; init; }

    /// <summary>Raw porcelain XY status code.</summary>
    public string StatusCode { get; init; } = "";
}

/// <summary>Grouped working tree status.</summary>
public sealed class WorkingTreeStatus
{
    /// <summary>Staged paths.</summary>
    public IReadOnlyList<WorkingTreeEntry> Staged { get; init; } = [];

    /// <summary>Unstaged paths.</summary>
    public IReadOnlyList<WorkingTreeEntry> Unstaged { get; init; } = [];

    /// <summary>Untracked paths.</summary>
    public IReadOnlyList<WorkingTreeEntry> Untracked { get; init; } = [];
}

/// <summary>Log / detail commit summary.</summary>
public sealed class CommitInfo
{
    /// <summary>Full sha.</summary>
    public required string Sha { get; init; }

    /// <summary>Short sha.</summary>
    public required string ShortSha { get; init; }

    /// <summary>Subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Body (optional).</summary>
    public string? Body { get; init; }

    /// <summary>Author name.</summary>
    public string? AuthorName { get; init; }

    /// <summary>Author email.</summary>
    public string? AuthorEmail { get; init; }

    /// <summary>Author date ISO.</summary>
    public string? AuthorAt { get; init; }

    /// <summary>Parent shas.</summary>
    public IReadOnlyList<string> Parents { get; init; } = [];
}

/// <summary>Commit detail with file change summary.</summary>
public sealed class CommitDetail
{
    /// <summary>Commit info.</summary>
    public required CommitInfo Commit { get; init; }

    /// <summary>Files added.</summary>
    public int FilesAdded { get; init; }

    /// <summary>Files deleted.</summary>
    public int FilesDeleted { get; init; }

    /// <summary>Files modified.</summary>
    public int FilesModified { get; init; }

    /// <summary>Changed paths.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];
}

/// <summary>One unified-diff hunk line.</summary>
public sealed class DiffLine
{
    /// <summary>Prefix: space, +, or -.</summary>
    public required char Kind { get; init; }

    /// <summary>Line text without prefix.</summary>
    public required string Text { get; init; }
}

/// <summary>A diff hunk.</summary>
public sealed class DiffHunk
{
    /// <summary>Header (@@ … @@).</summary>
    public required string Header { get; init; }

    /// <summary>Lines.</summary>
    public IReadOnlyList<DiffLine> Lines { get; init; } = [];
}

/// <summary>Diff for one path.</summary>
public sealed class DiffFile
{
    /// <summary>Path (new side when renamed).</summary>
    public required string Path { get; init; }

    /// <summary>Old path when renamed.</summary>
    public string? OldPath { get; init; }

    /// <summary>Hunks.</summary>
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

    /// <summary>Whether binary.</summary>
    public bool IsBinary { get; init; }
}

/// <summary>Parsed unified diff document.</summary>
public sealed class DiffDocument
{
    /// <summary>Files.</summary>
    public IReadOnlyList<DiffFile> Files { get; init; } = [];
}

/// <summary>One stash entry.</summary>
public sealed class StashEntry
{
    /// <summary>stash@{n} index.</summary>
    public required int Index { get; init; }

    /// <summary>Message.</summary>
    public required string Message { get; init; }

    /// <summary>Optional timestamp.</summary>
    public string? At { get; init; }

    /// <summary>Optional stash commit sha.</summary>
    public string? Sha { get; init; }
}

/// <summary>Options for create-branch.</summary>
public sealed class CreateBranchOptions
{
    /// <summary>Branch name.</summary>
    public required string Name { get; init; }

    /// <summary>Base ref (default HEAD / main).</summary>
    public string? BaseRef { get; init; }

    /// <summary>Checkout after create.</summary>
    public bool Checkout { get; init; } = true;
}

/// <summary>Options for pull.</summary>
public sealed class PullOptions
{
    /// <summary>Remote name.</summary>
    public string Remote { get; init; } = "origin";

    /// <summary>When true (default), uses --ff-only.</summary>
    public bool FfOnly { get; init; } = true;
}

/// <summary>Options for push.</summary>
public sealed class PushOptions
{
    /// <summary>Remote name.</summary>
    public string Remote { get; init; } = "origin";

    /// <summary>Set upstream (-u).</summary>
    public bool SetUpstream { get; init; }

    /// <summary>Never force by default; force requires explicit true.</summary>
    public bool Force { get; init; }
}

/// <summary>Options for commit log / graph fetch.</summary>
public sealed class CommitLogOptions
{
    /// <summary>Max commits (default 200).</summary>
    public int MaxCount { get; init; } = 200;

    /// <summary>When true, only first parent.</summary>
    public bool FirstParent { get; init; }
}
