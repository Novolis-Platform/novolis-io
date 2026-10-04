using Novolis.IO.Paths;

namespace Novolis.IO.Unit;

public sealed class CheckoutRootTests
{
    [Test]
    public async Task Resolve_uses_explicit_path()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-checkout-");
        try
        {
            var resolved = CheckoutRoot.Resolve(temp.FullName);
            await Assert.That(resolved).IsEqualTo(Path.GetFullPath(temp.FullName));
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Test]
    public async Task Resolve_walks_to_platform_marker()
    {
        var temp = Directory.CreateTempSubdirectory("novolis-checkout-walk-");
        try
        {
            File.WriteAllText(Path.Combine(temp.FullName, "Novolis.Platform.slnx"), "");
            var nested = Path.Combine(temp.FullName, "child");
            Directory.CreateDirectory(nested);
            var previous = Directory.GetCurrentDirectory();
            var previousEnv = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
            try
            {
                Environment.SetEnvironmentVariable("NOVOLIS_ROOT", null);
                Directory.SetCurrentDirectory(nested);
                var resolved = CheckoutRoot.Resolve();
                await Assert.That(resolved).IsEqualTo(temp.FullName);
            }
            finally
            {
                Directory.SetCurrentDirectory(previous);
                Environment.SetEnvironmentVariable("NOVOLIS_ROOT", previousEnv);
            }
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
