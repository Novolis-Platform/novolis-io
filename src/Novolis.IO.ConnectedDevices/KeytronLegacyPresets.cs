namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Useful mappings for the exact factory-firmware 4×4 Keytron.
/// </summary>
public static class KeytronLegacyPresets
{
    /// <summary>
    /// Creates a safe, useful 25-slot map for 16 keys and three knobs.
    /// </summary>
    /// <remarks>
    /// The first twelve keys become F13–F24, avoiding the factory Ctrl+A
    /// mapping while leaving those keys easy to bind in Windows applications.
    /// Knob direction is intentionally swapped from the attached unit's
    /// inverted volume behavior: clockwise is volume down and counter-clockwise
    /// is volume up. No firmware-update or reset action is included.
    /// </remarks>
    public static IReadOnlyList<KeytronLegacyAction> CreateUsefulMap()
    {
        var actions = new List<KeytronLegacyAction>(25);

        for (var usage = 0x68; usage <= 0x73; usage++)
        {
            actions.Add(KeytronLegacyAction.Keyboard(0, (byte)usage));
        }

        actions.Add(KeytronLegacyAction.Keyboard(0, 0x28)); // Enter
        actions.Add(KeytronLegacyAction.Keyboard(0x01, 0x2C)); // Ctrl+Space
        actions.Add(KeytronLegacyAction.Keyboard(0x01, 0x2B)); // Ctrl+Tab
        actions.Add(KeytronLegacyAction.Keyboard(0x03, 0x2B)); // Ctrl+Shift+Tab

        actions.AddRange(
        [
            // Knob 1: play/pause, corrected volume direction.
            KeytronLegacyAction.Consumer(0x00CD),
            KeytronLegacyAction.Consumer(0x00EA),
            KeytronLegacyAction.Consumer(0x00E9),

            // Knob 2: next track and brightness.
            KeytronLegacyAction.Consumer(0x00B5),
            KeytronLegacyAction.Consumer(0x0070),
            KeytronLegacyAction.Consumer(0x006F),

            // Knob 3: window and browser-tab navigation.
            KeytronLegacyAction.Keyboard(0x04, 0x2B), // Alt+Tab
            KeytronLegacyAction.Keyboard(0x01, 0x2B), // Ctrl+Tab
            KeytronLegacyAction.Keyboard(0x03, 0x2B), // Ctrl+Shift+Tab
        ]);

        return actions;
    }
}
