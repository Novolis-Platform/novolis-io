using System.IO.Abstractions;
using RootWorkspace = Novolis.IO.Workspace.IWorkspace;

namespace Novolis.IO.Git;

/// <summary>An arbitrary collection of Git repository workspaces with no shared-root guarantee.</summary>
public sealed record GitRepositoryWorkspaceSet(IReadOnlyList<GitRepositoryWorkspace> Members);
