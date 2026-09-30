using Novolis.IO.Paths;
using Novolis.IO.Recovery;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class RecoveryExtendedTests
{
    [Test]
    public async Task WriteSnapshot_trims_to_max_per_document()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-recovery-trim-");
        try
        {
            var store = new ContentRecoveryStore(temp.FullName, maxSnapshotsPerDocument: 2);
            store.WriteSnapshot("doc", "v1");
            store.WriteSnapshot("doc", "v2");
            store.WriteSnapshot("doc", "v3");
            var latest = store.GetLatest("doc");
            await Assert.That(latest).IsNotNull();
            await Assert.That(latest!.Content).IsEqualTo("v3");
            var dir = Directory.GetDirectories(temp.FullName);
            await Assert.That(dir.Length).IsEqualTo(1);
            var mdFiles = Directory.GetFiles(dir[0], "*.md");
            await Assert.That(mdFiles.Length).IsLessThanOrEqualTo(2);
            await Assert.That(mdFiles.Length).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task WriteSnapshot_isolates_documents_by_key()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-recovery-keys-");
        try
        {
            var store = new ContentRecoveryStore(temp.FullName);
            store.WriteSnapshot("alpha", "a");
            store.WriteSnapshot("beta", "b");
            await Assert.That(store.GetLatest("alpha")!.Content).IsEqualTo("a");
            await Assert.That(store.GetLatest("beta")!.Content).IsEqualTo("b");
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
