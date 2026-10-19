namespace Novolis.IO.Mobile.Android;

/// <summary>Result of a read-only Android inventory collection.</summary>
public sealed record AndroidInventoryResult
{
    /// <summary>Whether the inventory was collected successfully.</summary>
    public required bool Ok { get; init; }

    /// <summary>Collected inventory when at least the device snapshot succeeded.</summary>
    public AndroidDeviceInventory? Inventory { get; init; }

    /// <summary>Non-fatal command failures or compatibility warnings.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Fatal failure, when collection could not produce an inventory.</summary>
    public AndroidFailure? Failure { get; init; }
}
