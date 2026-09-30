namespace Novolis.IO.Git;

/// <summary>Options for create-branch.</summary>
public sealed class CreateBranchOptions
{
    /// <summary>Branch name.</summary>
    public required string Name { get; init; }

    /// <summary>Base ref (default HEAD / main).</summary>
    public string? BaseRef { get; init; }

    /// <summary>Checkout after create.</summary>
    public bool Checkout { get; init; } = true;
}
