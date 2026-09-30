using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.IO.GitHub;

/// <summary>Result of polling for an OAuth access token.</summary>
public sealed class DeviceTokenResult
{
    /// <summary>Creates a success or pending/failure result.</summary>
    public DeviceTokenResult(bool success, string? accessToken, string? error, string? errorDescription)
    {
        Success = success;
        AccessToken = accessToken;
        Error = error;
        ErrorDescription = errorDescription;
    }

    /// <summary>Whether an access token was issued.</summary>
    public bool Success { get; }

    /// <summary>OAuth access token when <see cref="Success"/>.</summary>
    public string? AccessToken { get; }

    /// <summary>OAuth error code (<c>authorization_pending</c>, <c>slow_down</c>, etc.).</summary>
    public string? Error { get; }

    /// <summary>Human-readable error detail.</summary>
    public string? ErrorDescription { get; }

    /// <summary>True when the user has not finished authorizing yet.</summary>
    public bool IsPending =>
        string.Equals(Error, "authorization_pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Error, "slow_down", StringComparison.OrdinalIgnoreCase);
}
