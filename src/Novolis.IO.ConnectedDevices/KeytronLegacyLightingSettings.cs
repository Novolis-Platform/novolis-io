namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// The lighting settings accepted by the factory-firmware Keytron.
/// </summary>
/// <param name="Mode">Device-specific animation mode number.</param>
/// <param name="Brightness">Brightness from 0 to 255.</param>
/// <param name="Speed">Animation speed from 0 to 255.</param>
/// <param name="Custom">Whether the custom HSV color is enabled.</param>
/// <param name="Hue">HSV hue scaled to 0 through 255.</param>
/// <param name="Saturation">HSV saturation scaled to 0 through 255.</param>
/// <param name="Value">HSV value scaled to 0 through 255.</param>
public readonly record struct KeytronLegacyLightingSettings(
    byte Mode,
    byte Brightness,
    byte Speed,
    bool Custom,
    byte Hue,
    byte Saturation,
    byte Value)
{
    /// <summary>
    /// A conservative custom red setting useful for confirming that writes work.
    /// </summary>
    public static KeytronLegacyLightingSettings Default =>
        new(1, 0x40, 0, true, 0, 0xFF, 0xFF);

    /// <summary>
    /// Converts the settings to the 13 bytes after the factory lighting command.
    /// </summary>
    public byte[] ToCommandSettings() =>
    [
        0x00,
        0x00,
        0x01,
        0x00,
        Mode,
        Brightness,
        Speed,
        0x00,
        Custom ? (byte)0x01 : (byte)0x00,
        0x00,
        Hue,
        Saturation,
        Value,
    ];

    /// <summary>
    /// Creates custom HSV settings from an RGB color while preserving the
    /// selected mode, brightness, and animation speed.
    /// </summary>
    public static KeytronLegacyLightingSettings FromRgb(
        byte red,
        byte green,
        byte blue,
        byte mode = 1,
        byte brightness = 0x40,
        byte speed = 0)
    {
        var redUnit = red / 255d;
        var greenUnit = green / 255d;
        var blueUnit = blue / 255d;
        var maximum = Math.Max(redUnit, Math.Max(greenUnit, blueUnit));
        var minimum = Math.Min(redUnit, Math.Min(greenUnit, blueUnit));
        var delta = maximum - minimum;

        var hue = 0d;
        if (delta > 0)
        {
            hue = maximum switch
            {
                var value when value == redUnit =>
                    60d * (((greenUnit - blueUnit) / delta) % 6d),
                var value when value == greenUnit =>
                    60d * (((blueUnit - redUnit) / delta) + 2d),
                _ =>
                    60d * (((redUnit - greenUnit) / delta) + 4d),
            };
            if (hue < 0)
            {
                hue += 360d;
            }
        }

        var saturation = maximum <= 0 ? 0d : delta / maximum;
        return new(
            mode,
            brightness,
            speed,
            true,
            ToByte(hue / 360d * 255d),
            ToByte(saturation * 255d),
            ToByte(maximum * 255d));
    }

    /// <summary>
    /// Converts the stored HSV color to an RGB triplet.
    /// </summary>
    public (byte Red, byte Green, byte Blue) ToRgb()
    {
        var hue = Hue / 255d * 360d;
        var saturation = Saturation / 255d;
        var value = Value / 255d;
        var chroma = value * saturation;
        var sector = hue / 60d;
        var second = chroma * (1d - Math.Abs(sector % 2d - 1d));
        var match = value - chroma;
        var (red, green, blue) = sector switch
        {
            < 1d => (chroma, second, 0d),
            < 2d => (second, chroma, 0d),
            < 3d => (0d, chroma, second),
            < 4d => (0d, second, chroma),
            < 5d => (second, 0d, chroma),
            _ => (chroma, 0d, second),
        };

        return (
            ToByte((red + match) * 255d),
            ToByte((green + match) * 255d),
            ToByte((blue + match) * 255d));
    }

    private static byte ToByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
