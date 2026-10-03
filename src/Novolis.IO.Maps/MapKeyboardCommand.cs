namespace Novolis.IO.Maps;

/// <summary>Platform-neutral commands a host can map from keyboard input.</summary>
public enum MapKeyboardCommand
{
    /// <summary>Copy the selected coordinate as invariant text.</summary>
    CopyCoordinate,

    /// <summary>Copy the selected coordinate and marker identity as JSON.</summary>
    CopyJson,

    /// <summary>Zoom in around the viewport center.</summary>
    ZoomIn,

    /// <summary>Zoom out around the viewport center.</summary>
    ZoomOut,

    /// <summary>Pan the camera left.</summary>
    PanLeft,

    /// <summary>Pan the camera right.</summary>
    PanRight,

    /// <summary>Pan the camera up.</summary>
    PanUp,

    /// <summary>Pan the camera down.</summary>
    PanDown,

    /// <summary>Complete the active drawing.</summary>
    CompleteDrawing,

    /// <summary>Cancel the active drawing.</summary>
    CancelDrawing,

    /// <summary>Request erasure of the currently selected overlay.</summary>
    EraseSelectedOverlay,
}
