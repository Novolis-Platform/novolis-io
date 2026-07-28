namespace Novolis.IO.Git;

/// <summary>Result of running a git process.</summary>
public sealed record GitProcessResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Runs git with the given arguments in a working directory.</summary>
public interface IGitProcessRunner
{
    /// <summary>Executes <c>git</c> with <paramref name="args"/>.</summary>
    GitProcessResult Run(string workingDirectory, params string[] args);
}

/// <summary>Default <see cref="IGitProcessRunner"/> using <c>git</c> on PATH.</summary>
public sealed class ProcessGitRunner : IGitProcessRunner
{
    /// <inheritdoc />
    public GitProcessResult Run(string workingDirectory, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start git.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitProcessResult(process.ExitCode, stdout, stderr);
    }
}
