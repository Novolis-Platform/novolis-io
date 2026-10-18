namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Useful mappings for the exact factory-firmware 4×4 Keytron.
/// </summary>
public static class KeytronLegacyPresets
{
    /// <summary>
    /// Creates a safe 25-slot test map for 16 keys and three knobs.
    /// </summary>
    /// <remarks>
    /// The first twelve keys become F13–F24, avoiding ordinary text entry and
    /// application shortcuts. The remaining four keys and every knob action
    /// use consumer/media controls only. Knob direction is intentionally
    /// swapped from the attached unit's inverted volume behavior: clockwise
    /// is volume down and counter-clockwise is volume up. No firmware-update
    /// or reset action is included.
    /// </remarks>
    public static IReadOnlyList<KeytronLegacyAction> CreateUsefulMap()
    {
        var actions = new List<KeytronLegacyAction>(25);

        for (var usage = 0x68; usage <= 0x73; usage++)
        {
            actions.Add(KeytronLegacyAction.Keyboard(0, (byte)usage));
        }

        // Keys 13–16: consumer controls rather than keyboard shortcuts.
        actions.Add(KeytronLegacyAction.Consumer(0x00CD)); // Play/pause
        actions.Add(KeytronLegacyAction.Consumer(0x00B5)); // Next track
        actions.Add(KeytronLegacyAction.Consumer(0x00B6)); // Previous track
        actions.Add(KeytronLegacyAction.Consumer(0x00E2)); // Mute

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

            // Knob 3: consumer controls only.
            KeytronLegacyAction.Consumer(0x00E2), // Mute
            KeytronLegacyAction.Consumer(0x00B6), // Previous track
            KeytronLegacyAction.Consumer(0x00B5), // Next track
        ]);

        return actions;
    }
}
