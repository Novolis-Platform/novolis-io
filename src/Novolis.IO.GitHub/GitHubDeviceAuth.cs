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

/// <summary>GitHub OAuth Device Authorization Grant client.</summary>
public sealed class GitHubDeviceAuth
{
    static readonly Uri DeviceCodeEndpoint = new("https://github.com/login/device/code");
    static readonly Uri TokenEndpoint = new("https://github.com/login/oauth/access_token");
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    readonly HttpClient _http;

    /// <summary>Creates a client using a dedicated <see cref="HttpClient"/>.</summary>
    public GitHubDeviceAuth(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.Accept.Any(h => h.MediaType == "application/json"))
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Novolis.IO.GitHub");
    }

    /// <summary>Requests a device/user code pair for <paramref name="clientId"/>.</summary>
    /// <remarks>
    /// OAuth Apps use <paramref name="scope"/> (e.g. <c>repo</c>).
    /// GitHub App client ids (<c>Iv1.</c>…) must not send scopes.
    /// </remarks>
    public async Task<DeviceCodeResponse> RequestDeviceCodeAsync(
        string clientId,
        string? scope = "repo",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var fields = new Dictionary<string, string> { ["client_id"] = clientId };
        var isGitHubApp = clientId.StartsWith("Iv", StringComparison.OrdinalIgnoreCase);
        if (!isGitHubApp && !string.IsNullOrWhiteSpace(scope))
            fields["scope"] = scope;

        using var content = new FormUrlEncodedContent(fields);
        using var response = await _http.PostAsync(DeviceCodeEndpoint, content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Device code request failed ({(int)response.StatusCode}): {body}");

        var dto = JsonSerializer.Deserialize<DeviceCodeDto>(body, JsonOptions)
            ?? throw new InvalidOperationException("Device code response was empty.");
        if (string.IsNullOrWhiteSpace(dto.DeviceCode) || string.IsNullOrWhiteSpace(dto.UserCode)
            || string.IsNullOrWhiteSpace(dto.VerificationUri))
            throw new InvalidOperationException($"Incomplete device code response: {body}");

        var verify = new Uri(dto.VerificationUri);
        Uri complete;
        if (!string.IsNullOrWhiteSpace(dto.VerificationUriComplete)
            && Uri.TryCreate(dto.VerificationUriComplete, UriKind.Absolute, out var parsedComplete))
        {
            complete = parsedComplete;
        }
        else
        {
            var builder = new UriBuilder(verify)
            {
                Query = "user_code=" + Uri.EscapeDataString(dto.UserCode),
            };
            complete = builder.Uri;
        }

        return new DeviceCodeResponse
        {
            DeviceCode = dto.DeviceCode,
            UserCode = dto.UserCode,
            VerificationUri = verify,
            VerificationUriComplete = complete,
            Interval = TimeSpan.FromSeconds(Math.Max(1, dto.Interval)),
            ExpiresInSeconds = dto.ExpiresIn,
        };
    }

    /// <summary>Polls once for an access token.</summary>
    public async Task<DeviceTokenResult> PollForTokenAsync(
        string clientId,
        string deviceCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceCode);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["device_code"] = deviceCode,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
        });
        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var dto = JsonSerializer.Deserialize<TokenDto>(body, JsonOptions)
            ?? throw new InvalidOperationException("Token response was empty.");

        if (!string.IsNullOrWhiteSpace(dto.AccessToken))
            return new DeviceTokenResult(true, dto.AccessToken, null, null);

        return new DeviceTokenResult(false, null, dto.Error, dto.ErrorDescription);
    }

    /// <summary>
    /// Polls until a token is issued, the device code expires, or <paramref name="cancellationToken"/> fires.
    /// </summary>
    public async Task<string> WaitForAccessTokenAsync(
        string clientId,
        DeviceCodeResponse device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, device.ExpiresInSeconds));
        var interval = device.Interval;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await PollForTokenAsync(clientId, device.DeviceCode, cancellationToken).ConfigureAwait(false);
            if (result.Success && !string.IsNullOrWhiteSpace(result.AccessToken))
                return result.AccessToken;

            if (!result.IsPending)
                throw new InvalidOperationException(result.ErrorDescription ?? result.Error ?? "Device authorization failed.");

            if (string.Equals(result.Error, "slow_down", StringComparison.OrdinalIgnoreCase))
                interval += TimeSpan.FromSeconds(5);

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("GitHub device authorization timed out.");
    }

    sealed class DeviceCodeDto
    {
        [JsonPropertyName("device_code")]
        public string? DeviceCode { get; set; }

        [JsonPropertyName("user_code")]
        public string? UserCode { get; set; }

        [JsonPropertyName("verification_uri")]
        public string? VerificationUri { get; set; }

        [JsonPropertyName("verification_uri_complete")]
        public string? VerificationUriComplete { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    sealed class TokenDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}
