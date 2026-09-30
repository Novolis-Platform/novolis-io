namespace Novolis.IO.Git;

/// <summary>Result of running a git process.</summary>
public sealed record GitProcessResult(int ExitCode, string StdOut, string StdErr);
