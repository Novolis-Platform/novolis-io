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
