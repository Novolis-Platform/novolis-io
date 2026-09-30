namespace Novolis.IO.Git;

/// <summary>Runs git with the given arguments in a working directory.</summary>
public interface IGitProcessRunner
{
    /// <summary>Executes <c>git</c> with <paramref name="args"/>.</summary>
    GitProcessResult Run(string workingDirectory, params string[] args);
}
