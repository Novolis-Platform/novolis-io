using Octokit;

namespace Novolis.IO.GitHub;

/// <summary>Obsolete alias for <see cref="SparseRepoMirrorOptions"/>.</summary>
[Obsolete("Use SparseRepoMirrorOptions.")]
public sealed class BooksRepoMirrorOptions
{
    /// <inheritdoc cref="SparseRepoMirrorOptions.Owner"/>
    public required string Owner { get; init; }

    /// <inheritdoc cref="SparseRepoMirrorOptions.Name"/>
    public required string Name { get; init; }

    /// <inheritdoc cref="SparseRepoMirrorOptions.WorkspaceRoot"/>
    public required string WorkspaceRoot { get; init; }

    /// <inheritdoc cref="SparseRepoMirrorOptions.ContentPrefix"/>
    public string ContentPrefix { get; init; } = "content/";

    /// <summary>Converts to <see cref="SparseRepoMirrorOptions"/>.</summary>
    public SparseRepoMirrorOptions ToSparse() => new()
    {
        Owner = Owner,
        Name = Name,
        WorkspaceRoot = WorkspaceRoot,
        ContentPrefix = ContentPrefix,
    };
}

/// <summary>Obsolete alias for <see cref="SparseRepoMirror"/>.</summary>
[Obsolete("Use SparseRepoMirror.")]
public sealed class BooksRepoMirror
{
    readonly SparseRepoMirror _inner;

    /// <summary>Creates a books-named wrapper around <see cref="SparseRepoMirror"/>.</summary>
    public BooksRepoMirror(GitHubClient client, BooksRepoMirrorOptions options)
        => _inner = new SparseRepoMirror(client, options.ToSparse());

    /// <summary>Creates a books-named wrapper around <see cref="SparseRepoMirror"/>.</summary>
    public BooksRepoMirror(GitHubClient client, SparseRepoMirrorOptions options)
        => _inner = new SparseRepoMirror(client, options);

    /// <inheritdoc cref="SparseRepoMirror.CreateClient"/>
    public static GitHubClient CreateClient(string accessToken, string productHeader = "Novolis.IO.GitHub") =>
        SparseRepoMirror.CreateClient(accessToken, productHeader);

    /// <inheritdoc cref="SparseRepoMirror.NoteDirty"/>
    public void NoteDirty(string relativePath) => _inner.NoteDirty(relativePath);

    /// <inheritdoc cref="SparseRepoMirror.DirtyCount"/>
    public int DirtyCount => _inner.DirtyCount;

    /// <inheritdoc cref="SparseRepoMirror.Branch"/>
    public string? Branch => _inner.Branch;

    /// <inheritdoc cref="SparseRepoMirror.PullAsync"/>
    public Task<MirrorPullResult> PullAsync(CancellationToken cancellationToken = default) =>
        _inner.PullAsync(cancellationToken);

    /// <inheritdoc cref="SparseRepoMirror.SaveCommitPushAsync"/>
    public Task<MirrorPushResult> SaveCommitPushAsync(string? message = null, CancellationToken cancellationToken = default) =>
        _inner.SaveCommitPushAsync(message, cancellationToken);
}