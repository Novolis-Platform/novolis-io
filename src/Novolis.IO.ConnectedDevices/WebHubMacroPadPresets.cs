namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Useful starting mappings for the three rotary controls on the full Keytron.
/// </summary>
public static class WebHubMacroPadPresets
{
    /// <summary>
    /// Creates a media-and-workspace map for the nine knob controls.
    /// The order is press, clockwise, counter-clockwise for each knob.
    /// </summary>
    /// <remarks>
    /// This intentionally fixes the common reversed-volume mapping and avoids
    /// making every knob press a mute toggle.
    /// </remarks>
    public static IReadOnlyList<WebHubMacroPadAction> CreateUsefulKnobActions() =>
    [
        WebHubMacroPadAction.Media(0x00CD),
        WebHubMacroPadAction.Media(0x00E9),
        WebHubMacroPadAction.Media(0x00EA),

        WebHubMacroPadAction.Media(0x00B7),
        WebHubMacroPadAction.Media(0x00B5),
        WebHubMacroPadAction.Media(0x00B6),

        WebHubMacroPadAction.Keyboard(0x04, 0x2B),
        WebHubMacroPadAction.Media(0x006F),
        WebHubMacroPadAction.Media(0x0070),
    ];
}
