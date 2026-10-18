namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Converts factory-firmware Keytron actions into useful descriptions.
/// </summary>
public static class KeytronLegacyActionDescriptions
{
    /// <summary>
    /// Gives the factory action type a meaningful operator-facing name while
    /// retaining the numeric type code for unknown firmware values.
    /// </summary>
    public static string DescribeType(KeytronLegacyActionType type) =>
        type switch
        {
            KeytronLegacyActionType.Disabled => "Disabled — no output (0x00)",
            KeytronLegacyActionType.BareKeyboard =>
                "Keyboard usage — legacy bare key (0x01)",
            KeytronLegacyActionType.Keyboard =>
                "Keyboard usage + modifier mask (0x02)",
            KeytronLegacyActionType.Consumer =>
                "Consumer/media control (0x03)",
            KeytronLegacyActionType.Mouse =>
                "Mouse action (0x04)",
            KeytronLegacyActionType.System =>
                "System-control usage (0x05)",
            _ => $"Unknown factory action type (0x{(byte)type:X2})",
        };

    /// <summary>
    /// Describes one action while retaining unknown type and byte values.
    /// </summary>
    public static string Describe(KeytronLegacyAction action) =>
        action.Type switch
        {
            KeytronLegacyActionType.Disabled => "No action",
            KeytronLegacyActionType.BareKeyboard or KeytronLegacyActionType.Keyboard =>
                $"Keyboard: {DescribeModifiers(action.Parameter1)}" +
                DescribeKeyboardUsage(action.Parameter2),
            KeytronLegacyActionType.Consumer =>
                $"Media: {DescribeConsumer((ushort)(action.Parameter1 | action.Parameter2 << 8))}",
            KeytronLegacyActionType.Mouse =>
                $"Mouse: 0x{action.Parameter1:X2} 0x{action.Parameter2:X2}",
            KeytronLegacyActionType.System =>
                $"System usage 0x{action.Parameter2:X2}{action.Parameter1:X2}",
            _ =>
                $"Type 0x{(byte)action.Type:X2}: " +
                $"{action.Parameter1:X2} {action.Parameter2:X2}",
        };

    /// <summary>
    /// Names the two type-specific bytes instead of presenting them as
    /// unexplained parameter numbers.
    /// </summary>
    public static string DescribeParameters(KeytronLegacyAction action) =>
        action.Type switch
        {
            KeytronLegacyActionType.Disabled =>
                $"unused1=0x{action.Parameter1:X2}, unused2=0x{action.Parameter2:X2}",
            KeytronLegacyActionType.BareKeyboard or KeytronLegacyActionType.Keyboard =>
                $"modifierMask=0x{action.Parameter1:X2}, " +
                $"keyboardUsage=0x{action.Parameter2:X2}",
            KeytronLegacyActionType.Consumer =>
                $"consumerUsage=0x{action.Parameter2:X2}{action.Parameter1:X2}",
            KeytronLegacyActionType.Mouse =>
                $"mouseByte1=0x{action.Parameter1:X2}, " +
                $"mouseByte2=0x{action.Parameter2:X2}",
            KeytronLegacyActionType.System =>
                $"systemUsage=0x{action.Parameter2:X2}{action.Parameter1:X2}",
            _ =>
                $"unknownByte1=0x{action.Parameter1:X2}, " +
                $"unknownByte2=0x{action.Parameter2:X2}",
        };

    private static string DescribeModifiers(byte modifier)
    {
        var modifiers = new List<string>(4);
        if ((modifier & 0x01) != 0)
            modifiers.Add("Ctrl");
        if ((modifier & 0x02) != 0)
            modifiers.Add("Shift");
        if ((modifier & 0x04) != 0)
            modifiers.Add("Alt");
        if ((modifier & 0x08) != 0)
            modifiers.Add("GUI");

        return modifiers.Count == 0
            ? string.Empty
            : $"{string.Join(" + ", modifiers)} + ";
    }

    private static string DescribeConsumer(ushort usage) =>
        usage switch
        {
            0x00B5 => "Next track",
            0x00B6 => "Previous track",
            0x00B7 => "Stop",
            0x00CD => "Play / pause",
            0x00E2 => "Mute",
            0x00E9 => "Volume up",
            0x00EA => "Volume down",
            0x006F => "Brightness up",
            0x0070 => "Brightness down",
            _ => $"Consumer usage 0x{usage:X4}",
        };

    private static string DescribeKeyboardUsage(byte usage) =>
        usage switch
        {
            >= 0x04 and <= 0x1D => ((char)('A' + usage - 0x04)).ToString(),
            >= 0x1E and <= 0x27 => usage == 0x27
                ? "0"
                : ((char)('1' + usage - 0x1E)).ToString(),
            0x28 => "Enter",
            0x29 => "Escape",
            0x2A => "Backspace",
            0x2B => "Tab",
            0x2C => "Space",
            0x39 => "Caps Lock",
            >= 0x3A and <= 0x45 => $"F{usage - 0x39}",
            0x46 => "Print Screen",
            0x49 => "Insert",
            0x4A => "Home",
            0x4B => "Page Up",
            0x4C => "Delete",
            0x4D => "End",
            0x4E => "Page Down",
            0x4F => "Right",
            0x50 => "Left",
            0x51 => "Down",
            0x52 => "Up",
            >= 0x68 and <= 0x73 => $"F{usage - 0x5B}",
            _ => $"Keyboard usage 0x{usage:X2}",
        };
}
