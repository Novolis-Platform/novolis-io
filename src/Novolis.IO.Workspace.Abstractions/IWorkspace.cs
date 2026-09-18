using System.IO.Abstractions;

namespace Novolis.IO.Workspace;

/// <summary>
/// A typed root directory for a workspace-shaped resource.
/// </summary>
/// <remarks>
/// This interface deliberately represents only a directory root. Structured project workspaces,
/// .NET solutions, Git repositories, and domain content roots compose this contract without being
/// treated as the same concept.
/// </remarks>
public interface IWorkspace
{
    /// <summary>Root directory represented by this workspace.</summary>
    IDirectoryInfo Root { get; }
}
