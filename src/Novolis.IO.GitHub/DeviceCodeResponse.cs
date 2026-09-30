using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.IO.GitHub;

/// <summary>Device-code response from GitHub OAuth device flow.</summary>
public sealed class DeviceCodeResponse
{
    /// <summary>Device code used when polling for the token.</summary>
    public required string DeviceCode { get; init; }

    /// <summary>User code shown to the user.</summary>
    public required string UserCode { get; init; }

    /// <summary>Verification URL to open (GitHub app / browser / passkey).</summary>
    public required Uri VerificationUri { get; init; }

    /// <summary>
    /// Prefer this URL when opening a browser — includes the user code so GitHub can skip manual entry.
    /// </summary>
    public Uri VerificationUriComplete { get; init; } = null!;

    /// <summary>Recommended polling interval.</summary>
    public required TimeSpan Interval { get; init; }

    /// <summary>Seconds until the device code expires.</summary>
    public required int ExpiresInSeconds { get; init; }
}
