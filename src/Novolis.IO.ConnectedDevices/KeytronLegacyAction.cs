namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// One three-byte action stored by the factory-firmware Keytron.
/// </summary>
/// <param name="Type">The legacy action type.</param>
/// <param name="Parameter1">The first type-specific byte.</param>
/// <param name="Parameter2">The second type-specific byte.</param>
public readonly record struct KeytronLegacyAction(
    KeytronLegacyActionType Type,
    byte Parameter1,
    byte Parameter2)
{
    /// <summary>
    /// An empty legacy entry.
    /// </summary>
    public static KeytronLegacyAction Disabled =>
        new(KeytronLegacyActionType.Disabled, 0, 0);

    /// <summary>
    /// Creates a keyboard action. The modifier mask uses the USB-HID left-side
    /// convention: Ctrl=01, Shift=02, Alt=04, GUI=08.
    /// </summary>
    public static KeytronLegacyAction Keyboard(byte modifier, byte usage) =>
        new(KeytronLegacyActionType.Keyboard, modifier, usage);

    /// <summary>
    /// Creates a consumer/media action from a little-endian usage value.
    /// </summary>
    public static KeytronLegacyAction Consumer(ushort usage) =>
        new(KeytronLegacyActionType.Consumer, (byte)usage, (byte)(usage >> 8));

    /// <summary>
    /// Returns the exact three-byte factory-firmware representation.
    /// </summary>
    public byte[] ToByteArray() => [ (byte)Type, Parameter1, Parameter2 ];

    /// <summary>
    /// Provides an operator-facing interpretation without hiding raw bytes.
    /// </summary>
    public string Description => KeytronLegacyActionDescriptions.Describe(this);

    /// <summary>
    /// Creates an action from its three-byte representation.
    /// </summary>
    public static KeytronLegacyAction FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 3)
        {
            throw new ArgumentException(
                "A factory-firmware action requires three bytes.",
                nameof(bytes));
        }

        return new(
            (KeytronLegacyActionType)bytes[0],
            bytes[1],
            bytes[2]);
    }

    public override string ToString() => Description;
}
