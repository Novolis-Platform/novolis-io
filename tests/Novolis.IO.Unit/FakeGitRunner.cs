using Novolis.IO.Git;
using Novolis.IO.Paths;
using Novolis.IO.Recovery;

namespace Novolis.IO.Unit;

sealed class FakeGitRunner : IGitProcessRunner
{
    readonly Dictionary<string, GitProcessResult> _map = new(StringComparer.Ordinal);

    public void Set(string[] args, int exitCode, string stdout, string stderr = "") =>
        _map[string.Join('\0', args)] = new GitProcessResult(exitCode, stdout, stderr);

    public GitProcessResult Run(string workingDirectory, params string[] args)
    {
        var key = string.Join('\0', args);
        if (_map.TryGetValue(key, out var result))
            return result;
        return new GitProcessResult(1, "", "unexpected: " + string.Join(' ', args));
    }
}
