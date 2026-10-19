using Novolis.IO.Mobile.Android;

namespace Novolis.IO.Unit;

public sealed class AndroidInventoryTests
{
    [Test]
    public async Task ParsePackages_ClassifiesSystemAndDisabledPackages()
    {
        const string packages = """
            package:/system/app/Settings/Settings.apk=com.android.settings versionCode:28
            package:/data/app/~~abc==/shell.apk=com.novolis.shell versionCode:7
            """;
        const string system = """
            package:com.android.settings
            """;
        const string disabled = """
            package:com.novolis.shell
            """;

        var parsed = AndroidDeviceInventoryCollector.ParsePackages(
            packages,
            system,
            disabled);

        await Assert.That(parsed).Count().IsEqualTo(2);
        var settings = parsed.Single(package => package.PackageName == "com.android.settings");
        await Assert.That(settings.IsSystem).IsTrue();
        await Assert.That(settings.IsEnabled).IsTrue();
        await Assert.That(settings.VersionCode).IsEqualTo(28);

        var shell = parsed.Single(package => package.PackageName == "com.novolis.shell");
        await Assert.That(shell.IsSystem).IsFalse();
        await Assert.That(shell.IsEnabled).IsFalse();
    }

    [Test]
    public async Task ParseLauncherActivities_TracksPriorityAndDefault()
    {
        const string output = """
            2 activities found:
              Activity #0:
                priority=0 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=true
                com.novolis.shell/.MainActivity
              Activity #1:
                priority=10 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=false
                com.android.settings/.Settings
            """;

        var parsed = AndroidDeviceInventoryCollector.ParseLauncherActivities(output);

        await Assert.That(parsed).Count().IsEqualTo(2);
        await Assert.That(parsed[0].PackageName).IsEqualTo("com.novolis.shell");
        await Assert.That(parsed[0].ActivityName).IsEqualTo(".MainActivity");
        await Assert.That(parsed[0].IsDefault).IsTrue();
        await Assert.That(parsed[1].Priority).IsEqualTo(10);
        await Assert.That(parsed[1].IsDefault).IsFalse();
    }

    [Test]
    public async Task ParseAccounts_DoesNotRetainAccountNames()
    {
        const string output = """
            Accounts: 1
            Account {name=person@example.test type=com.google}
            RegisteredServicesCache:
              ServiceInfo: AuthenticatorDescription {type=com.google}, ComponentInfo{com.google/.Service}
            """;

        var parsed = AndroidDeviceInventoryCollector.ParseAccounts(output);

        await Assert.That(parsed.AccountCount).IsEqualTo(1);
        await Assert.That(parsed.AccountTypes).Contains("com.google");
        await Assert.That(parsed.AuthenticatorTypes).Contains("com.google");
        await Assert.That(parsed.ToString()).DoesNotContain("person@example.test");
    }

    [Test]
    public async Task ParseSettings_NormalizesNullValues()
    {
        var parsed = AndroidDeviceInventoryCollector.ParseSettings(
        [
            new KeyValuePair<string, string?>("global.device_provisioned", "1"),
            new KeyValuePair<string, string?>("secure.backup_enabled", "null"),
        ]);

        await Assert.That(parsed.Values["global.device_provisioned"]).IsEqualTo("1");
        await Assert.That(parsed.Values["secure.backup_enabled"]).IsNull();
    }
}
