namespace Novolis.IO.ConnectedDevices;

/// <summary>
/// Action types understood by the WebHub/SDCX macro-pad firmware.
/// </summary>
public enum WebHubMacroPadActionType : byte
{
    MouseButton = 0x10,
    MouseMove = 0x11,
    Disabled = 0x13,
    Keyboard = 0x20,
    Media = 0x30,
    Power = 0x40,
    Macro = 0x60,
    OpenWebsite = 0x80,
}
