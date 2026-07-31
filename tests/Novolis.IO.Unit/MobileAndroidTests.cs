using Novolis.IO.Mobile.Android;

namespace Novolis.IO.Unit;

public sealed class MobileAndroidTests
{
    [Test]
    public async Task ParseDevices_ReadsSerialStateAndTags()
    {
        const string stdout = """
            List of devices attached
            emulator-5554          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64xa transport_id:1
            R58M12ABCDE            unauthorized
            """;

        var devices = AndroidDebugBridge.ParseDevices(stdout);
        await Assert.That(devices.Count).IsEqualTo(2);
        await Assert.That(devices[0].Serial).IsEqualTo("emulator-5554");
        await Assert.That(devices[0].State).IsEqualTo(AdbDeviceState.Device);
        await Assert.That(devices[0].Model).IsEqualTo("sdk_gphone64_x86_64");
        await Assert.That(devices[0].TransportId).IsEqualTo("1");
        await Assert.That(devices[1].Serial).IsEqualTo("R58M12ABCDE");
        await Assert.That(devices[1].State).IsEqualTo(AdbDeviceState.Unauthorized);
    }

    [Test]
    public async Task ParseBattery_ReadsLevelAndPower()
    {
        const string raw = """
            Current Battery Service state:
              AC powered: false
              USB powered: true
              level: 76
              scale: 100
              status: 2
              temperature: 369
              voltage: 4130
              technology: Li-ion
            """;

        var b = AndroidDebugBridge.ParseBattery(raw);
        await Assert.That(b.Level).IsEqualTo(76);
        await Assert.That(b.UsbPowered).IsTrue();
        await Assert.That(b.TemperatureCelsius).IsEqualTo(36.9);
        await Assert.That(b.StatusLabel).IsEqualTo("charging");
    }

    [Test]
    public async Task ParseState_MapsKnownTokens()
    {
        await Assert.That(AndroidDebugBridge.ParseState("device")).IsEqualTo(AdbDeviceState.Device);
        await Assert.That(AndroidDebugBridge.ParseState("offline")).IsEqualTo(AdbDeviceState.Offline);
        await Assert.That(AndroidDebugBridge.ParseState("mystery")).IsEqualTo(AdbDeviceState.Unknown);
    }

    [Test]
    public async Task ApkValidator_RejectsMissingAndAcceptsZipApk()
    {
        var missing = ApkValidator.Validate(Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.apk"));
        await Assert.That(missing.Ok).IsFalse();

        var path = Path.Combine(Path.GetTempPath(), $"novolis-apk-{Guid.NewGuid():N}.apk");
        try
        {
            await using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            {
                zip.CreateEntry("AndroidManifest.xml");
                zip.CreateEntry("classes.dex");
                // Pad so size clears MinApkBytes default (1024).
                var pad = zip.CreateEntry("assets/pad.bin");
                await using var s = pad.Open();
                await s.WriteAsync(new byte[2048]);
            }

            var ok = ApkValidator.Validate(path, new ApkInstallOptions { MinApkBytes = 64 });
            if (!ok.Ok)
                throw new Exception(string.Join("; ", ok.Errors));
            await Assert.That(ok.Ok).IsTrue();
            await Assert.That(ok.SizeBytes).IsGreaterThan(0);
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Test]
    public async Task ParsePackageInfo_ReadsPmPathAndVersions()
    {
        var info = AndroidAppInstaller.ParsePackageInfo(
            "com.novolis.booksmobile",
            "package:/data/app/~~x==/com.novolis.booksmobile-y==/base.apk\n",
            "    versionCode=1 minSdk=23 targetSdk=36\n    versionName=0.1.0\n");
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.IsInstalled).IsTrue();
        await Assert.That(info.VersionName).IsEqualTo("0.1.0");
        await Assert.That(info.VersionCode).IsEqualTo(1);
        var flags = AndroidAppInstaller.BuildInstallArgs(new ApkInstallOptions
        {
            Reinstall = true,
            GrantPermissions = true,
            AllowDowngrade = true,
        });
        await Assert.That(flags).Contains("-r");
        await Assert.That(flags).Contains("-g");
        await Assert.That(flags).Contains("-d");
    }
}
