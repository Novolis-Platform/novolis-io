namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// One four-byte macro-pad action record.
/// </summary>
/// <param name="Type">The action type byte.</param>
/// <param name="Parameter1">The first type-specific parameter.</param>
/// <param name="Parameter2">The second type-specific parameter.</param>
/// <param name="Parameter3">The third type-specific parameter.</param>
public readonly record struct WebHubMacroPadAction(
    WebHubMacroPadActionType Type,
    byte Parameter1,
    byte Parameter2,
    byte Parameter3)
{
    /// <summary>
    /// An action that does nothing.
    /// </summary>
    public static WebHubMacroPadAction Disabled => new(WebHubMacroPadActionType.Disabled, 0, 0, 0);

    /// <summary>
    /// Creates a keyboard action. Parameter 1 is the modifier byte and parameter 2
    /// is the HID keyboard usage.
    /// </summary>
    public static WebHubMacroPadAction Keyboard(byte modifier, byte usage) =>
        new(WebHubMacroPadActionType.Keyboard, modifier, usage, 0);

    /// <summary>
    /// Creates a media action from the little-endian consumer usage value.
    /// </summary>
    public static WebHubMacroPadAction Media(ushort usage) =>
        new(WebHubMacroPadActionType.Media, (byte)usage, (byte)(usage >> 8), 0);

    /// <summary>
    /// Returns the record in the firmware's four-byte representation.
    /// </summary>
    public byte[] ToByteArray() => [ (byte)Type, Parameter1, Parameter2, Parameter3 ];

    /// <summary>
    /// Creates an action from a four-byte firmware record.
    /// </summary>
    public static WebHubMacroPadAction FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
        {
            throw new ArgumentException("An action record requires four bytes.", nameof(bytes));
        }

        return new((WebHubMacroPadActionType)bytes[0], bytes[1], bytes[2], bytes[3]);
    }

    public override string ToString() =>
        Type switch
        {
            WebHubMacroPadActionType.Disabled => "Disabled",
            WebHubMacroPadActionType.Keyboard => $"Keyboard 0x{Parameter2:X2} (mod 0x{Parameter1:X2})",
            WebHubMacroPadActionType.Media => $"Media 0x{Parameter2:X2}{Parameter1:X2}",
            WebHubMacroPadActionType.MouseButton => $"Mouse button 0x{Parameter3:X2}{Parameter2:X2}{Parameter1:X2}",
            WebHubMacroPadActionType.MouseMove => $"Mouse move {Parameter1:X2}/{Parameter2:X2}/{Parameter3:X2}",
            WebHubMacroPadActionType.Power => $"Power 0x{Parameter1:X2}",
            WebHubMacroPadActionType.Macro => $"Macro {Parameter1} (repeat {Parameter2})",
            WebHubMacroPadActionType.OpenWebsite => $"Website {Parameter1:X2} {Parameter2:X2} {Parameter3:X2}",
            _ => $"Type 0x{(byte)Type:X2} {Parameter1:X2} {Parameter2:X2} {Parameter3:X2}",
        };
}
