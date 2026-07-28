using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>Git repository helper for status, checkpoint, passes, and tags.</summary>
public sealed class GitRepositoryService
{
    readonly IGitProcessRunner _runner;
    readonly string _passStoreRelativePath;

    /// <summary>Creates a service.</summary>
    /// <param name="runner">Git process runner.</param>
    /// <param name="passStoreRelativePath">Relative path for pass metadata JSON (default <c>.novolis/git-passes.json</c>).</param>
    public GitRepositoryService(IGitProcessRunner? runner = null, string? passStoreRelativePath = null)
    {
        _runner = runner ?? new ProcessGitRunner();
        _passStoreRelativePath = string.IsNullOrWhiteSpace(passStoreRelativePath)
            ? Path.Combine(".novolis", "git-passes.json")
            : passStoreRelativePath;
    }

    /// <summary>Reads repository status.</summary>
    public GitStatus GetStatus(string repoRoot)
    {
        EnsureGitRepo(repoRoot);
        var branch = RunText(repoRoot, "rev-parse", "--abbrev-ref", "HEAD").Trim();
        var dirty = RunText(repoRoot, "status", "--porcelain").Trim();
        var upstreamRaw = RunText(repoRoot, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}").Trim();
        var upstream = upstreamRaw.Contains("fatal", StringComparison.OrdinalIgnoreCase) ? null : upstreamRaw;
        var aheadBehind = upstream is null
            ? (ahead: 0, behind: 0)
            : ParseAheadBehind(RunText(repoRoot, "rev-list", "--left-right", "--count", $"{upstream}...HEAD").Trim());

        var passes = PassStore.Load(repoRoot, _passStoreRelativePath);
        var lastCommit = RunText(repoRoot, "log", "-1", "--format=%cI|%h|%s").Trim();
        string? lastAt = null, lastSha = null, lastMsg = null;
        if (!string.IsNullOrEmpty(lastCommit) && lastCommit.Contains('|'))
        {
            var parts = lastCommit.Split('|', 3);
            lastAt = parts[0];
            lastSha = parts[1];
            lastMsg = parts.Length > 2 ? parts[2] : null;
        }

        return new GitStatus
        {
            Branch = branch,
            Upstream = upstream,
            Ahead = aheadBehind.ahead,
            Behind = aheadBehind.behind,
            Dirty = !string.IsNullOrEmpty(dirty),
            DirtyFiles = dirty.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
            ActivePass = passes.ActivePass,
            LastCommitAt = lastAt,
            LastCommitSha = lastSha,
            LastCommitMessage = lastMsg
        };
    }

    /// <summary>Stages, commits, and optionally pushes a checkpoint.</summary>
    public GitOperationResult Checkpoint(string repoRoot, string message, CheckpointOptions? options = null)
    {
        EnsureGitRepo(repoRoot);
        if (string.IsNullOrWhiteSpace(message))
            return GitOperationResult.Fail("checkpoint", "Message is required.");

        options ??= new CheckpointOptions();
        GitProcessResult add;
        if (options.Pathspecs is { Count: > 0 })
        {
            var args = new List<string> { "add", "--" };
            args.AddRange(options.Pathspecs);
            add = _runner.Run(repoRoot, args.ToArray());
        }
        else
        {
            add = _runner.Run(repoRoot, "add", "-A");
        }

        if (add.ExitCode != 0)
            return GitOperationResult.Fail("checkpoint", add.StdErr.Trim(), add);

        var commit = _runner.Run(repoRoot, "commit", "-m", message, "--no-verify");
        if (commit.ExitCode != 0)
        {
            if (commit.StdOut.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
                || commit.StdErr.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
                return GitOperationResult.Fail("checkpoint", "Nothing to commit.", commit);
            return GitOperationResult.Fail("checkpoint", commit.StdErr.Trim(), commit);
        }

        var sha = RunText(repoRoot, "rev-parse", "HEAD").Trim();
        if (options.Push)
        {
            var push = _runner.Run(repoRoot, "push");
            if (push.ExitCode != 0)
                return GitOperationResult.Fail("checkpoint", $"Committed {sha} but push failed: {push.StdErr.Trim()}", new { sha, push });
        }

        return GitOperationResult.Success("checkpoint", "Checkpoint committed.", new { sha, message });
    }

    /// <summary>Starts a named pass branch and records it in the pass store.</summary>
    public GitOperationResult PassStart(string repoRoot)
    {
        EnsureGitRepo(repoRoot);
        var passes = PassStore.Load(repoRoot, _passStoreRelativePath);
        if (!string.IsNullOrEmpty(passes.ActivePass))
            return GitOperationResult.Fail("pass start", $"Pass already active: {passes.ActivePass}.");

        var id = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var branch = $"pass/{id}";
        var create = _runner.Run(repoRoot, "checkout", "-b", branch);
        if (create.ExitCode != 0)
            return GitOperationResult.Fail("pass start", create.StdErr.Trim(), create);

        var push = _runner.Run(repoRoot, "push", "-u", "origin", branch);
        if (push.ExitCode != 0)
            return GitOperationResult.Fail("pass start", push.StdErr.Trim(), push);

        passes.Passes.Add(new PassEntry(id, branch, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), null));
        passes.ActivePass = id;
        PassStore.Save(repoRoot, _passStoreRelativePath, passes);
        return GitOperationResult.Success("pass start", $"Started pass {id}.", new { id, branch });
    }

    /// <summary>Merges the active pass into the default branch.</summary>
    public GitOperationResult PassFinish(string repoRoot)
    {
        EnsureGitRepo(repoRoot);
        var passes = PassStore.Load(repoRoot, _passStoreRelativePath);
        if (string.IsNullOrEmpty(passes.ActivePass))
            return GitOperationResult.Fail("pass finish", "No active pass.");

        var active = passes.Passes.FirstOrDefault(p => p.Id == passes.ActivePass)
                     ?? throw new InvalidOperationException($"Active pass not found: {passes.ActivePass}");
        var defaultBranch = DetectDefaultBranch(repoRoot);

        var checkout = _runner.Run(repoRoot, "checkout", defaultBranch);
        if (checkout.ExitCode != 0)
            return GitOperationResult.Fail("pass finish", checkout.StdErr.Trim(), checkout);

        var merge = _runner.Run(repoRoot, "merge", active.Branch, "-m", $"Merge pass {active.Id}");
        if (merge.ExitCode != 0)
            return GitOperationResult.Fail("pass finish", merge.StdErr.Trim(), merge);

        var push = _runner.Run(repoRoot, "push", "origin", defaultBranch);
        if (push.ExitCode != 0)
            return GitOperationResult.Fail("pass finish", push.StdErr.Trim(), push);

        active.FinishedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        passes.ActivePass = null;
        PassStore.Save(repoRoot, _passStoreRelativePath, passes);
        return GitOperationResult.Success("pass finish", $"Merged pass {active.Id} into {defaultBranch}.",
            new { passId = active.Id, branch = active.Branch, mergedInto = defaultBranch });
    }

    /// <summary>Creates and pushes an annotated-style lightweight tag on the default branch.</summary>
    public GitOperationResult CreateRevisionTag(string repoRoot, string tag)
    {
        EnsureGitRepo(repoRoot);
        if (string.IsNullOrWhiteSpace(tag))
            return GitOperationResult.Fail("revision create", "Tag is required.");

        var defaultBranch = DetectDefaultBranch(repoRoot);
        var checkout = _runner.Run(repoRoot, "checkout", defaultBranch);
        if (checkout.ExitCode != 0)
            return GitOperationResult.Fail("revision create", checkout.StdErr.Trim(), checkout);

        var tagResult = _runner.Run(repoRoot, "tag", tag, defaultBranch);
        if (tagResult.ExitCode != 0)
            return GitOperationResult.Fail("revision create", tagResult.StdErr.Trim(), tagResult);

        var push = _runner.Run(repoRoot, "push", "origin", tag);
        if (push.ExitCode != 0)
            return GitOperationResult.Fail("revision create", push.StdErr.Trim(), push);

        return GitOperationResult.Success("revision create", $"Tagged {tag} on {defaultBranch}.", new { tag, branch = defaultBranch });
    }

    string DetectDefaultBranch(string repoRoot)
    {
        foreach (var name in new[] { "main", "master" })
        {
            var r = _runner.Run(repoRoot, "rev-parse", "--verify", name);
            if (r.ExitCode == 0)
                return name;
        }

        var sym = RunText(repoRoot, "symbolic-ref", "refs/remotes/origin/HEAD").Trim();
        if (sym.StartsWith("refs/remotes/origin/", StringComparison.Ordinal))
            return sym["refs/remotes/origin/".Length..];
        return "main";
    }

    static (int ahead, int behind) ParseAheadBehind(string line)
    {
        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2
            && int.TryParse(parts[0], out var behind)
            && int.TryParse(parts[1], out var ahead))
            return (ahead, behind);
        return (0, 0);
    }

    void EnsureGitRepo(string repoRoot)
    {
        var r = _runner.Run(repoRoot, "rev-parse", "--git-dir");
        if (r.ExitCode != 0)
            throw new InvalidOperationException("Not a git repository.");
    }

    string RunText(string repoRoot, params string[] args)
    {
        var r = _runner.Run(repoRoot, args);
        if (r.ExitCode != 0 && !string.IsNullOrWhiteSpace(r.StdErr))
            return r.StdErr;
        return r.StdOut;
    }
}

sealed class PassStore
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static PassesFile Load(string repoRoot, string relativePath)
    {
        var path = Path.Combine(repoRoot, relativePath);
        if (!File.Exists(path))
            return new PassesFile();
        return JsonSerializer.Deserialize<PassesFile>(File.ReadAllText(path), JsonOpts) ?? new PassesFile();
    }

    public static void Save(string repoRoot, string relativePath, PassesFile file)
    {
        var path = Path.Combine(repoRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOpts));
    }
}

sealed class PassesFile
{
    public string? ActivePass { get; set; }
    public List<PassEntry> Passes { get; set; } = [];
}

sealed class PassEntry
{
    public PassEntry() { }

    public PassEntry(string id, string branch, string startedAt, string? finishedAt)
    {
        Id = id;
        Branch = branch;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
    }

    public string Id { get; set; } = "";
    public string Branch { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string? FinishedAt { get; set; }
}
