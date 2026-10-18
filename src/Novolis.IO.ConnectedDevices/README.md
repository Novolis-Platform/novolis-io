<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-io/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-io/) · [Source](https://github.com/Novolis-Platform/novolis-io)
<!-- novolis-pkg-brand:end -->

# Novolis.IO.ConnectedDevices

Cross-platform discovery of connected HID interfaces, including USB
keyboards, macro pads, knob controllers, and composite peripherals. The
package uses [HidSharp](https://www.nuget.org/packages/HidSharp/) and leaves
device-specific report interpretation to the host.

## Install

```bash
dotnet add package Novolis.IO.ConnectedDevices
```

## Usage

### Enumerate devices

```csharp
using Novolis.IO.ConnectedDevices;

using var catalog = new ConnectedDeviceCatalog();

foreach (var device in catalog.EnumerateHidDevices())
{
    var info = device.Descriptor;
    Console.WriteLine(
        $"{info.HardwareId} {info.FriendlyName} " +
        $"input={info.MaxInputReportLength} path={info.DevicePath}");
}
```

This prints every HID interface visible to the current account. A composite
peripheral may appear more than once, with one interface for its keyboard
surface and another for its controls.

## Select a controller and read reports

Use the product name after the initial enumeration, or pin the device by its
vendor/product identifiers when they are known.

```csharp
var controller = catalog.FindHidDevice(
    new ConnectedDeviceFilter
    {
        ProductNameContains = "knob",
        // VendorId = 0x1234,
        // ProductId = 0x5678,
    });

if (controller is not null)
{
    using var stream = controller.OpenStream();
    var buffer = new byte[Math.Max(1, controller.Descriptor.MaxInputReportLength)];
    var bytesRead = await stream.ReadAsync(buffer);
    Console.WriteLine(Convert.ToHexString(buffer.AsSpan(0, bytesRead)));
}
```

`OpenStream` returns raw HID reports. Knob, key, LED, and vendor-specific
report layouts are intentionally not guessed by this package; decode the
bytes according to the controller's report descriptor. Some operating systems
own keyboard-class interfaces; use `TryOpenStream` when a composite device
contains an interface that may be unavailable for application access.

## Configure the full 4x4 Keytron macro pad

The WebHub/SDCX family exposes a separate vendor HID interface for its 16 keys
and three rotary controls. The exact full-size target is `6D7D:DCFC`; a
different HID interface from the same physical device is the ordinary keyboard.

```csharp
var macroPad = WebHubMacroPad.Find(catalog);

if (macroPad is null)
{
    Console.WriteLine("Keytron was not found.");
}
else if (!macroPad.SupportsWebHubFrames)
{
    Console.WriteLine(macroPad.ProtocolSupportDescription);
}
else
{
    using var session = macroPad.OpenSession();
    var info = session.ReadDeviceInfo();
    var actions = session.ReadActions();
    Console.WriteLine(
        $"profile={info.ActiveProfile} actions={actions.Count}");
}
```

`WebHubMacroPadSession` supports read-only device/configuration reads and
explicit action, profile, and backlight writes for interfaces with the
documented 64-byte report shape. `WebHubMacroPadAction.Description` provides an
operator-facing interpretation of the four bytes, and
`WebHubMacroPadPresets.CreateUsefulKnobActions()` supplies a starting map with
counter-clockwise volume down, clockwise volume up, and useful media/workspace
press actions. `WebHubMacroPad.ProbeReadOnly()` can safely inspect a mismatched
interface without enabling writes. The API deliberately does not expose
factory-reset or bootloader commands.

The exact factory-firmware `6D7D:DCFC` interface is handled by
`KeytronLegacySession`. It supports the verified three-byte action table and
the factory lighting mode command: mode, brightness, speed, custom-color flag,
and HSV bytes are written as a 32-byte report-ID-2 transaction. Lighting
responses are retained as raw reports because this firmware's read response
does not echo the full setting record.

## Hot-plug changes

```csharp
catalog.DevicesChanged += (_, _) =>
{
    var current = catalog.EnumerateHidDevices();
    Console.WriteLine($"HID interfaces connected: {current.Count}");
};
```

The event does not identify the changed interface. Re-enumerate the catalog
to obtain current metadata.
