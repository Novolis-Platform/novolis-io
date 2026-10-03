namespace Novolis.IO.Maps;

/// <summary>
/// Opt-in interaction capabilities shared by Avalonia and MAUI map controls.
/// </summary>
public sealed record MapInteractionOptions
{
    /// <summary>Disables all optional capabilities.</summary>
    public static MapInteractionOptions Disabled { get; } = new();

    /// <summary>Enables copy shortcuts or host copy commands.</summary>
    public bool EnableClipboardShortcuts { get; init; }

    /// <summary>Enables keyboard camera commands where the host has focus.</summary>
    public bool EnableKeyboardNavigation { get; init; }

    /// <summary>Enables drawing sessions started by the host.</summary>
    public bool EnableDrawing { get; init; }

    /// <summary>Enables keyboard requests to erase the selected host-owned overlay.</summary>
    public bool EnableOverlayErasure { get; init; }

    /// <summary>Shows calculated measurements in the control while drawing.</summary>
    public bool ShowMeasurementResults { get; init; } = true;
}
