using System.Net;
using Octokit;

namespace Novolis.IO.GitHub;

/// <summary>Probes and classifies GitHub OAuth access-token failures.</summary>
public static class GitHubAccessToken
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="accessToken"/> can call the authenticated user API.
    /// </summary>
    public static async Task<bool> TryValidateAsync(
        string accessToken,
        string productHeader = "Novolis.IO.GitHub",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        cancellationToken.ThrowIfCancellationRequested();
        var client = SparseRepoMirror.CreateClient(accessToken, productHeader);
        try
        {
            _ = await client.User.Current().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && IsUnauthorized(ex))
        {
            return false;
        }
    }

    /// <summary>Whether an exception means the token is missing, expired, or rejected.</summary>
    public static bool IsUnauthorized(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (Exception? ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is AuthorizationException)
                return true;
            if (ex is ApiException api && api.StatusCode == HttpStatusCode.Unauthorized)
                return true;
            if (LooksLikeUnauthorizedMessage(ex.Message))
                return true;
        }

        return false;
    }

    /// <summary>Whether a GitHub error message indicates bad/expired credentials.</summary>
    public static bool LooksLikeUnauthorizedMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        return message.Contains("Bad credentials", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Requires authentication", StringComparison.OrdinalIgnoreCase)
               || (message.Contains("401", StringComparison.Ordinal)
                   && message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase));
    }
}
