using Novolis.IO.Git;

namespace Novolis.IO.Unit;

sealed class FlexibleGitRunner : IGitProcessRunner
{
    readonly List<(Func<string[], bool> match, GitProcessResult result)> _rules = [];

    public void When(Func<string[], bool> match, int exitCode, string stdout, string stderr = "") =>
        _rules.Add((match, new GitProcessResult(exitCode, stdout, stderr)));

    public GitProcessResult Run(string workingDirectory, params string[] args)
    {
        foreach (var (match, result) in _rules)
        {
            if (match(args))
                return result;
        }

        return new GitProcessResult(1, "", "unexpected: " + string.Join(' ', args));
    }
}
