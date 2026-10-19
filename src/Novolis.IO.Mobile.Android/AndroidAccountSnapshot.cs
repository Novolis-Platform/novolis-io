namespace Novolis.IO.Mobile.Android;

/// <summary>
/// Privacy-safe account inventory. Account names are deliberately not retained.
/// </summary>
public sealed record AndroidAccountSnapshot
{
    /// <summary>Number of configured accounts for the selected user.</summary>
    public int AccountCount { get; init; }

    /// <summary>Types of configured accounts, when Android reports them.</summary>
    public IReadOnlyList<string> AccountTypes { get; init; } = [];

    /// <summary>
    /// Account types registered by installed authenticator services. This is
    /// not a count of configured accounts.
    /// </summary>
    public IReadOnlyList<string> AuthenticatorTypes { get; init; } = [];
}
