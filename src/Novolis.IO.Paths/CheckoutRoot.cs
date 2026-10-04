namespace Novolis.IO.Paths;

/// <summary>
/// Resolves the Novolis checkout parent (sibling git repos) from an explicit
/// path, <c>NOVOLIS_ROOT</c>, or a walk for platform markers.
/// </summary>
public static class CheckoutRoot
{
    /// <summary>
    /// Resolves the checkout parent. An explicit path wins, then
    /// <c>NOVOLIS_ROOT</c> when that directory exists, then the nearest
    /// ancestor that contains <c>Novolis.Platform.slnx</c> or
    /// <c>novolis-governance</c>.
    /// </summary>
    public static string Resolve(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        var env = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        if (RootFinder.TryFind(
                Directory.GetCurrentDirectory(),
                IsCheckoutMarker,
                out var root))
        {
            return root;
        }

        throw new InvalidOperationException(
            "Could not resolve Novolis checkout root. Pass an explicit root or set NOVOLIS_ROOT.");
    }

    static bool IsCheckoutMarker(DirectoryInfo dir) =>
        File.Exists(Path.Combine(dir.FullName, "Novolis.Platform.slnx"))
        || Directory.Exists(Path.Combine(dir.FullName, "novolis-governance"));
}
