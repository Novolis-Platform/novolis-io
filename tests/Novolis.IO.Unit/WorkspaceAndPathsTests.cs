using Novolis.IO.Paths;
using Novolis.IO.Recovery;
using Novolis.IO.Workspace.Testing;

namespace Novolis.IO.Unit;

public sealed class RootFinderTests
{
    [Test]
    public async Task TryFind_returns_false_when_no_markers()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-root-");
        try
        {
            var nested = Path.Combine(temp.FullName, "deep", "nested");
            Directory.CreateDirectory(nested);
            var ok = RootFinder.TryFind(nested, ["missing.txt"], out var root);
            await Assert.That(ok).IsFalse();
            await Assert.That(root).IsEqualTo(nested);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task TryFind_predicate_matches_custom_root()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-root-predicate-");
        try
        {
            File.WriteAllText(Path.Combine(temp.FullName, "ROOT"), "x");
            var nested = Path.Combine(temp.FullName, "child");
            Directory.CreateDirectory(nested);
            var ok = RootFinder.TryFind(nested, dir => File.Exists(Path.Combine(dir.FullName, "ROOT")), out var root);
            await Assert.That(ok).IsTrue();
            await Assert.That(root).IsEqualTo(temp.FullName);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task TryFind_rejects_empty_marker_list()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-io-root-empty-");
        try
        {
            var ok = RootFinder.TryFind(temp.FullName, [], out _);
            await Assert.That(ok).IsFalse();
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
