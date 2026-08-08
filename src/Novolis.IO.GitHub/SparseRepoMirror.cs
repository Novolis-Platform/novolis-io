using System.Text.Json;
using System.Text.Json.Serialization;
using Octokit;

namespace Novolis.IO.GitHub;

/// <summary>Options for <see cref="SparseRepoMirror"/>.</summary>
public sealed class SparseRepoMirrorOptions
{
    /// <summary>Repository owner (e.g. <c>frankhaugen</c>).</summary>
    public required string Owner { get; init; }

    /// <summary>Repository name (e.g. <c>books</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Local workspace root that will contain <c>content/</c>.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Path prefix to mirror (default <c>content/</c>; NMP books use <c>src/</c>). Always also pulls root <c>manuscript.yaml</c> when present.</summary>
    public string ContentPrefix { get; init; } = "content/";
}

/// <summary>Result of a pull.</summary>
public sealed class MirrorPullResult
{
    /// <summary>Creates a pull result.</summary>
    public MirrorPullResult(
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

    /// <summary>Whether the pull succeeded.</summary>
    public bool Ok { get; }

    /// <summary>Human-readable status.</summary>
    public string Message { get; }

    /// <summary>Head commit SHA after pull.</summary>
    public string? CommitSha { get; }

    /// <summary>Number of files written.</summary>
    public int FileCount { get; }

    /// <summary>Token was rejected — host should clear credentials and ask the user to sign in again.</summary>
    public bool RequiresReauthentication { get; }
}

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

/// <summary>
/// Sparse GitHub mirror of a repository tree <c>content/</c> tree using the Git Data API.
/// </summary>
public sealed class SparseRepoMirror
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    readonly GitHubClient _client;
    readonly SparseRepoMirrorOptions _options;
    readonly string _statePath;

    /// <summary>Creates a mirror bound to an authenticated <see cref="GitHubClient"/>.</summary>
    public SparseRepoMirror(GitHubClient client, SparseRepoMirrorOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.WorkspaceRoot);
        Directory.CreateDirectory(options.WorkspaceRoot);
        var novolis = Path.Combine(options.WorkspaceRoot, ".novolis");
        Directory.CreateDirectory(novolis);
        _statePath = Path.Combine(novolis, "mobile-mirror.json");
    }

    /// <summary>Creates an Octokit client for a bearer token.</summary>
    public static GitHubClient CreateClient(string accessToken, string productHeader = "Novolis.IO.GitHub")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return new GitHubClient(new ProductHeaderValue(productHeader))
        {
            Credentials = new Credentials(accessToken),
        };
    }

    /// <summary>Marks a workspace-relative path dirty (to include in the next push).</summary>
    public void NoteDirty(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var state = LoadState();
        var normalized = NormalizeRel(relativePath);
        if (!state.Dirty.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            state.Dirty.Add(normalized);
        SaveState(state);
    }

    /// <summary>Number of dirty paths pending push.</summary>
    public int DirtyCount => LoadState().Dirty.Count;

    /// <summary>Current tracked branch name, if known.</summary>
    public string? Branch => LoadState().Branch;

    /// <summary>Pulls the remote <c>content/</c> tree into the workspace.</summary>
    public async Task<MirrorPullResult> PullAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var repo = await _client.Repository.Get(_options.Owner, _options.Name).ConfigureAwait(false);
            var branch = repo.DefaultBranch;
            var reference = await _client.Git.Reference.Get(_options.Owner, _options.Name, "heads/" + branch)
                .ConfigureAwait(false);
            var commitSha = reference.Object.Sha;
            var commit = await _client.Git.Commit.Get(_options.Owner, _options.Name, commitSha).ConfigureAwait(false);
            var tree = await _client.Git.Tree.GetRecursive(_options.Owner, _options.Name, commit.Tree.Sha)
                .ConfigureAwait(false);

            var prefix = NormalizePrefix(_options.ContentPrefix);
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var written = 0;

            foreach (var item in tree.Tree.Where(t => t.Type == TreeType.Blob))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = item.Path.Replace('\\', '/');
                var isWorkspaceMarker = string.Equals(path, "manuscript.yaml", StringComparison.OrdinalIgnoreCase);
                if (!isWorkspaceMarker
                    && !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(path, prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip large binary assets under **/Assets/ or **/assets/
                if (path.Contains("/assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IsTextish(path))
                    continue;

                var blob = await _client.Git.Blob.Get(_options.Owner, _options.Name, item.Sha).ConfigureAwait(false);
                var bytes = DecodeBlob(blob);
                var localPath = Path.Combine(_options.WorkspaceRoot, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                await File.WriteAllBytesAsync(localPath, bytes, cancellationToken).ConfigureAwait(false);
                files[path] = item.Sha;
                written++;
            }

            var state = new MirrorState
            {
                Branch = branch,
                CommitSha = commitSha,
                Files = files,
                Dirty = [],
            };
            SaveState(state);
            return new MirrorPullResult(true, $"Pulled {written} files from {branch}@{commitSha[..7]}.", commitSha, written);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var reauth = GitHubAccessToken.IsUnauthorized(ex);
            var message = reauth
                ? "GitHub session expired — sign in again."
                : ex.Message;
            return new MirrorPullResult(false, message, requiresReauthentication: reauth);
        }
    }

    /// <summary>
    /// Saves dirty workspace files, creates a commit on the tracked branch, and updates the remote ref
    /// (Save/Commit/Push in one step). Auto-message when <paramref name="message"/> is null.
    /// </summary>
    public async Task<MirrorPushResult> SaveCommitPushAsync(
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var state = LoadState();
            if (string.IsNullOrWhiteSpace(state.Branch) || string.IsNullOrWhiteSpace(state.CommitSha))
                return new MirrorPushResult(false, "Pull before Save/Commit/Push.");

            if (state.Dirty.Count == 0)
                return new MirrorPushResult(true, "Nothing to commit.", state.CommitSha, 0);

            var commitMessage = string.IsNullOrWhiteSpace(message)
                ? $"Sparse mirror {DateTime.Now:yyyy-MM-dd HH:mm}"
                : message.Trim();

            var newTreeItems = new List<NewTreeItem>();
            foreach (var rel in state.Dirty.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var localPath = Path.Combine(_options.WorkspaceRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localPath))
                    continue;

                var bytes = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
                var blob = await _client.Git.Blob.Create(
                        _options.Owner,
                        _options.Name,
                        new NewBlob
                        {
                            Content = Convert.ToBase64String(bytes),
                            Encoding = EncodingType.Base64,
                        })
                    .ConfigureAwait(false);

                newTreeItems.Add(new NewTreeItem
                {
                    Path = rel,
                    Mode = Octokit.FileMode.File,
                    Type = TreeType.Blob,
                    Sha = blob.Sha,
                });
                state.Files[rel] = blob.Sha;
            }

            if (newTreeItems.Count == 0)
            {
                state.Dirty.Clear();
                SaveState(state);
                return new MirrorPushResult(true, "Nothing to commit.", state.CommitSha, 0);
            }

            var parent = await _client.Git.Commit.Get(_options.Owner, _options.Name, state.CommitSha!)
                .ConfigureAwait(false);
            var newTree = new NewTree { BaseTree = parent.Tree.Sha };
            foreach (var item in newTreeItems)
                newTree.Tree.Add(item);

            var treeResponse = await _client.Git.Tree.Create(_options.Owner, _options.Name, newTree)
                .ConfigureAwait(false);
            var newCommit = await _client.Git.Commit.Create(
                    _options.Owner,
                    _options.Name,
                    new NewCommit(commitMessage, treeResponse.Sha, state.CommitSha))
                .ConfigureAwait(false);

            await _client.Git.Reference.Update(
                    _options.Owner,
                    _options.Name,
                    "heads/" + state.Branch,
                    new ReferenceUpdate(newCommit.Sha))
                .ConfigureAwait(false);

            state.CommitSha = newCommit.Sha;
            state.Dirty.Clear();
            SaveState(state);
            return new MirrorPushResult(
                true,
                $"Pushed {newTreeItems.Count} file(s) as {newCommit.Sha[..7]}.",
                newCommit.Sha,
                newTreeItems.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var reauth = GitHubAccessToken.IsUnauthorized(ex);
            var status = reauth
                ? "GitHub session expired — sign in again."
                : ex.Message;
            return new MirrorPushResult(false, status, requiresReauthentication: reauth);
        }
    }

    MirrorState LoadState()
    {
        if (!File.Exists(_statePath))
            return new MirrorState();
        try
        {
            var json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize<MirrorState>(json, JsonOptions) ?? new MirrorState();
        }
        catch
        {
            return new MirrorState();
        }
    }

    void SaveState(MirrorState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_statePath, json);
    }

    static string NormalizeRel(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    static string NormalizePrefix(string prefix)
    {
        var p = prefix.Replace('\\', '/').TrimStart('/');
        return p.EndsWith('/') ? p : p + "/";
    }

    static bool IsTextish(string path)
    {
        var ext = Path.GetExtension(path);
        return ext is ".md" or ".markdown" or ".yaml" or ".yml" or ".json" or ".txt" or ".csv"
            or ".svg" or ".html" or ".css" or ".js" or ".ts" or ".xml" or ".toml";
    }

    static byte[] DecodeBlob(Blob blob)
    {
        if (blob.Encoding == EncodingType.Base64)
            return Convert.FromBase64String(blob.Content);
        return System.Text.Encoding.UTF8.GetBytes(blob.Content ?? string.Empty);
    }

    sealed class MirrorState
    {
        public string? Branch { get; set; }
        public string? CommitSha { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("dirty")]
        public List<string> Dirty { get; set; } = [];
    }
}
