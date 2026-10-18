namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Converts WebHub action records into useful operator-facing descriptions.
/// </summary>
public static class WebHubMacroPadActionDescriptions
{
    /// <summary>
    /// Describes one four-byte action record without hiding its unknown values.
    /// </summary>
    public static string Describe(WebHubMacroPadAction action) =>
        action.Type switch
        {
            WebHubMacroPadActionType.Disabled => "No action",
            WebHubMacroPadActionType.Keyboard =>
                $"Keyboard: {DescribeModifiers(action.Parameter1)}{DescribeKeyboardUsage(action.Parameter2)}",
            WebHubMacroPadActionType.Media =>
                $"Media: {DescribeMedia((ushort)(action.Parameter1 | action.Parameter2 << 8))}",
            WebHubMacroPadActionType.MouseButton =>
                $"Mouse button 0x{action.Parameter3:X2}{action.Parameter2:X2}{action.Parameter1:X2}",
            WebHubMacroPadActionType.MouseMove =>
                $"Mouse move {action.Parameter1:X2}/{action.Parameter2:X2}/{action.Parameter3:X2}",
            WebHubMacroPadActionType.Power => $"Power 0x{action.Parameter1:X2}",
            WebHubMacroPadActionType.Macro =>
                $"Macro {action.Parameter1} (repeat {action.Parameter2})",
            WebHubMacroPadActionType.OpenWebsite =>
                $"Website {action.Parameter1:X2} {action.Parameter2:X2} {action.Parameter3:X2}",
            _ =>
                $"Unknown type 0x{(byte)action.Type:X2} " +
                $"{action.Parameter1:X2} {action.Parameter2:X2} {action.Parameter3:X2}",
        };

    /// <summary>
    /// Returns a type-specific parameter explanation alongside the action summary.
    /// </summary>
    public static string DescribeParameters(WebHubMacroPadAction action) =>
        action.Type switch
        {
            WebHubMacroPadActionType.Keyboard =>
                $"modifier=0x{action.Parameter1:X2}, usage=0x{action.Parameter2:X2}",
            WebHubMacroPadActionType.Media =>
                $"consumerUsage=0x{action.Parameter2:X2}{action.Parameter1:X2}",
            WebHubMacroPadActionType.MouseButton =>
                $"buttonMask=0x{action.Parameter3:X2}{action.Parameter2:X2}{action.Parameter1:X2}",
            WebHubMacroPadActionType.MouseMove =>
                $"x=0x{action.Parameter1:X2}, y=0x{action.Parameter2:X2}, wheel=0x{action.Parameter3:X2}",
            WebHubMacroPadActionType.Power =>
                $"powerCode=0x{action.Parameter1:X2}",
            WebHubMacroPadActionType.Macro =>
                $"macro=0x{action.Parameter1:X2}, repeat=0x{action.Parameter2:X2}, extra=0x{action.Parameter3:X2}",
            WebHubMacroPadActionType.OpenWebsite =>
                $"urlBytes={action.Parameter1:X2} {action.Parameter2:X2} {action.Parameter3:X2}",
            _ =>
                $"p1=0x{action.Parameter1:X2}, p2=0x{action.Parameter2:X2}, p3=0x{action.Parameter3:X2}",
        };

    private static string DescribeMedia(ushort usage) =>
        usage switch
        {
            0x00B0 => "Play",
            0x00B1 => "Pause",
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

    private static string DescribeModifiers(byte modifier)
    {
        if (modifier == 0)
        {
            return string.Empty;
        }

        var modifiers = new List<string>(6);
        if ((modifier & 0x01) != 0)
            modifiers.Add("Ctrl");
        if ((modifier & 0x02) != 0)
            modifiers.Add("Shift");
        if ((modifier & 0x04) != 0)
            modifiers.Add("Alt");
        if ((modifier & 0x08) != 0)
            modifiers.Add("Meta");
        if ((modifier & 0x10) != 0)
            modifiers.Add("Right Ctrl");
        if ((modifier & 0x20) != 0)
            modifiers.Add("Right Shift");
        if ((modifier & 0x40) != 0)
            modifiers.Add("Right Alt");
        if ((modifier & 0x80) != 0)
            modifiers.Add("Right Meta");

        return string.Join(" + ", modifiers) + " + ";
    }

    private static string DescribeKeyboardUsage(byte usage) =>
        usage switch
        {
            0x04 => "A",
            0x05 => "B",
            0x06 => "C",
            0x07 => "D",
            0x08 => "E",
            0x09 => "F",
            0x0A => "G",
            0x0B => "H",
            0x0C => "I",
            0x0D => "J",
            0x0E => "K",
            0x0F => "L",
            0x10 => "M",
            0x11 => "N",
            0x12 => "O",
            0x13 => "P",
            0x14 => "Q",
            0x15 => "R",
            0x16 => "S",
            0x17 => "T",
            0x18 => "U",
            0x19 => "V",
            0x1A => "W",
            0x1B => "X",
            0x1C => "Y",
            0x1D => "Z",
            0x1E => "1",
            0x1F => "2",
            0x20 => "3",
            0x21 => "4",
            0x22 => "5",
            0x23 => "6",
            0x24 => "7",
            0x25 => "8",
            0x26 => "9",
            0x27 => "0",
            0x28 => "Enter",
            0x29 => "Escape",
            0x2A => "Backspace",
            0x2B => "Tab",
            0x2C => "Space",
            0x39 => "Caps Lock",
            0x3A => "F1",
            0x3B => "F2",
            0x3C => "F3",
            0x3D => "F4",
            0x3E => "F5",
            0x3F => "F6",
            0x40 => "F7",
            0x41 => "F8",
            0x42 => "F9",
            0x43 => "F10",
            0x44 => "F11",
            0x45 => "F12",
            0x4F => "Right arrow",
            0x50 => "Left arrow",
            0x51 => "Down arrow",
            0x52 => "Up arrow",
            _ => $"Keyboard usage 0x{usage:X2}",
        };
}
