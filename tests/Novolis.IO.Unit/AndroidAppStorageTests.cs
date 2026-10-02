using Novolis.IO.Platform.Android;

namespace Novolis.IO.Unit;

public sealed class AndroidAppStorageTests
{
    [Test]
    public async Task SanitizeProductName_RejectsPathSegments()
    {
        await Assert.That(() => AndroidStorageLayout.SanitizeProductName("a/b"))
            .Throws<ArgumentException>();
        await Assert.That(() => AndroidStorageLayout.SanitizeProductName(".."))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task UnderOrganization_NestsNovolisAndProduct()
    {
        var root = AndroidStorageLayout.UnderOrganization(
            Path.Combine("storage", "Documents"),
            "BooksMobile");

        await Assert.That(root).IsEqualTo(Path.Combine("storage", "Documents", "Novolis", "BooksMobile"));
        await Assert.That(AndroidStorageLayout.Workspace(root))
            .IsEqualTo(Path.Combine(root, "workspace"));
        await Assert.That(AndroidStorageLayout.MediaStoreDocumentsRelative("BooksMobile"))
            .IsEqualTo("Documents/Novolis/BooksMobile");
    }

    [Test]
    public async Task DescribeForAdb_UsesPullForSharedDocuments()
    {
        var locations = new AndroidAppLocations(
            "BooksMobile",
            AndroidStorageKind.SharedDocuments,
            Path.Combine("Documents", "Novolis", "BooksMobile"),
            Path.Combine("Documents", "Novolis", "BooksMobile", "workspace"),
            Path.Combine("cache", "Novolis", "BooksMobile"),
            "com.novolis.booksmobile");

        await Assert.That(AndroidStorageLayout.DescribeForAdb(locations))
            .IsEqualTo($"adb pull \"{locations.RootDirectory}\"");
    }

    [Test]
    public async Task DescribeForAdb_UsesRunAsForAppExternal()
    {
        var locations = new AndroidAppLocations(
            "BooksMobile",
            AndroidStorageKind.AppExternal,
            Path.Combine("Android", "data", "com.novolis.booksmobile", "files", "Novolis", "BooksMobile"),
            Path.Combine("workspace"),
            Path.Combine("cache"),
            "com.novolis.booksmobile");

        await Assert.That(AndroidStorageLayout.DescribeForAdb(locations))
            .IsEqualTo($"adb shell run-as com.novolis.booksmobile ls \"{locations.RootDirectory}\"");
    }

    [Test]
    public async Task MoveLegacyTree_MovesPayloadAndLeavesExistingDestination()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "novolis-android-storage-" + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(scratch, "legacy");
        var destination = Path.Combine(scratch, "destination");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "note.txt"), "kept");

        try
        {
            AndroidStorageMigration.MoveLegacyTree(legacy, destination);
            await Assert.That(File.ReadAllText(Path.Combine(destination, "note.txt"))).IsEqualTo("kept");
            await Assert.That(Directory.Exists(legacy)).IsFalse();

            Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(legacy, "other.txt"), "new");
            AndroidStorageMigration.MoveLegacyTree(legacy, destination);
            await Assert.That(File.Exists(Path.Combine(destination, "other.txt"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(legacy, "other.txt"))).IsTrue();
        }
        finally
        {
            if (Directory.Exists(scratch))
                Directory.Delete(scratch, recursive: true);
        }
    }
}
