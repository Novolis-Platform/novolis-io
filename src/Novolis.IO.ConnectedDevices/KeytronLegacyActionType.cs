namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Three-byte action types used by the factory-firmware Keytron interface.
/// </summary>
public enum KeytronLegacyActionType : byte
{
    /// <summary>Raw/empty entry.</summary>
    Disabled = 0x00,

    /// <summary>A single keyboard usage with a legacy modifier mask.</summary>
    BareKeyboard = 0x01,

    /// <summary>A keyboard usage with a legacy modifier mask.</summary>
    Keyboard = 0x02,

    /// <summary>A little-endian consumer/media usage.</summary>
    Consumer = 0x03,

    /// <summary>A mouse button or wheel action.</summary>
    Mouse = 0x04,

    /// <summary>A system-control usage.</summary>
    System = 0x05,
}
