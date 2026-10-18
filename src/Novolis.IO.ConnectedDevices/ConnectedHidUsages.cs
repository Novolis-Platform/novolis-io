namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Common HID usage identifiers useful when selecting a logical collection.
/// </summary>
public static class ConnectedHidUsages
{
    /// <summary>Generic Desktop joystick collection.</summary>
    public const uint GenericDesktopJoystick = 0x0001_0004;

    /// <summary>Generic Desktop gamepad collection.</summary>
    public const uint GenericDesktopGamepad = 0x0001_0005;

    /// <summary>Generic Desktop keyboard collection.</summary>
    public const uint GenericDesktopKeyboard = 0x0001_0006;

    /// <summary>Generic Desktop X axis.</summary>
    public const uint GenericDesktopX = 0x0001_0030;

    /// <summary>Generic Desktop Y axis.</summary>
    public const uint GenericDesktopY = 0x0001_0031;

    /// <summary>Generic Desktop Z axis.</summary>
    public const uint GenericDesktopZ = 0x0001_0032;

    /// <summary>Generic Desktop dial.</summary>
    public const uint GenericDesktopDial = 0x0001_0037;

    /// <summary>Generic Desktop wheel.</summary>
    public const uint GenericDesktopWheel = 0x0001_0038;
}
