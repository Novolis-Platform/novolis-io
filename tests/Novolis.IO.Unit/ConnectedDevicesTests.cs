using Novolis.IO.ConnectedDevices;

namespace Novolis.IO.Unit;

public sealed class ConnectedDevicesTests
{
    [Test]
    public async Task Filter_matches_hardware_and_text_criteria()
    {
        var descriptor = CreateDescriptor();
        var filter = new ConnectedDeviceFilter
        {
            VendorId = 0x1234,
            ProductId = 0x5678,
            SerialNumber = "knob-01",
            ProductNameContains = "control",
            ManufacturerContains = "example",
            FriendlyNameContains = "macro",
        };

        await Assert.That(filter.Matches(descriptor)).IsTrue();
    }

    [Test]
    public async Task Filter_rejects_a_different_interface()
    {
        var descriptor = CreateDescriptor();
        var filter = new ConnectedDeviceFilter
        {
            VendorId = 0x1234,
            ProductId = 0x9999,
        };

        await Assert.That(filter.Matches(descriptor)).IsFalse();
    }

    [Test]
    public async Task Descriptor_formats_vendor_and_product_identifier()
    {
        await Assert.That(CreateDescriptor().HardwareId).IsEqualTo("1234:5678");
    }

    [Test]
    public async Task Catalog_enumerates_current_hid_interfaces()
    {
        using var catalog = new ConnectedDeviceCatalog();
        var devices = catalog.EnumerateHidDevices();

        foreach (var device in devices)
        {
            Console.WriteLine(
                $"HID {device.Descriptor.HardwareId} " +
                $"{device.Descriptor.FriendlyName} " +
                $"path={device.Descriptor.DevicePath}");
        }

        await Assert.That(devices).IsNotNull();
    }

    [Test]
    public async Task Macro_pad_action_round_trips_its_four_byte_record()
    {
        var action = WebHubMacroPadAction.Keyboard(0x02, 0x04);
        var copy = WebHubMacroPadAction.FromBytes(action.ToByteArray());

        await Assert.That(copy).IsEqualTo(action);
        await Assert.That(copy.ToString()).Contains("Keyboard");
    }

    [Test]
    public async Task Macro_pad_target_uses_the_full_size_4x4_hardware_id()
    {
        var filter = new ConnectedDeviceFilter
        {
            VendorId = WebHubMacroPad.VendorId,
            ProductId = WebHubMacroPad.ProductId,
        };
        var usages = new List<uint> { WebHubMacroPad.ConfigurationUsage };

        await Assert.That(filter.VendorId).IsEqualTo(0x6D7D);
        await Assert.That(filter.ProductId).IsEqualTo(0xDCFC);
        await Assert.That(usages).Contains(0xFF000001u);
    }

    private static ConnectedDeviceDescriptor CreateDescriptor() =>
        new()
        {
            DevicePath = "hid-test-path",
            VendorId = 0x1234,
            ProductId = 0x5678,
            SerialNumber = "knob-01",
            Manufacturer = "Example Controls",
            ProductName = "Control Keyboard",
            FriendlyName = "Example Macro Keyboard",
        };
}
