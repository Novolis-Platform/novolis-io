using System.Collections.Concurrent;
using System.Diagnostics;

namespace Novolis.IO.Processes;

/// <summary>Specification for an external process job.</summary>
public sealed class ProcessJobSpec
{
    /// <summary>Executable path or name.</summary>
    public required string FileName { get; init; }

    /// <summary>Arguments.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Working directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Display title.</summary>
    public string? Title { get; init; }
}
