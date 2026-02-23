using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class CognitoTokenManagementService(
  IOptions<AwsOptions> awsOptions,
  IHttpClientFactory httpClientFactory)
  : ICognitoTokenManagementService
{
  private readonly AwsOptions _awsOptions = awsOptions.Value;

  public async Task<SocialTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(refreshToken))
    {
      return new SocialTokenResult(false, "Refresh token is required.");
    }

    var appClientId = _awsOptions.Cognito.AppClientId?.Trim();
    if (string.IsNullOrWhiteSpace(appClientId))
    {
      return new SocialTokenResult(false, "Cognito AppClientId is not configured.");
    }

    var oauthBaseUrl = AuthenticationExtensions.ResolveCognitoOAuthBaseUrl(_awsOptions);
    var endpoint = new Uri($"{oauthBaseUrl.TrimEnd('/')}/oauth2/token");

    var form = new Dictionary<string, string>
    {
      ["grant_type"] = "refresh_token",
      ["client_id"] = appClientId,
      ["refresh_token"] = refreshToken.Trim()
    };

    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
      Content = new FormUrlEncodedContent(form)
    };

    var client = httpClientFactory.CreateClient(CognitoOAuthTokenClient.HttpClientName);
    using var response = await client.SendAsync(request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      return new SocialTokenResult(false, "Failed to refresh token.");
    }

    var payload = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: cancellationToken);

    var accessToken = payload.TryGetProperty("access_token", out var at) ? at.GetString() : null;
    var idToken = payload.TryGetProperty("id_token", out var it) ? it.GetString() : null;
    var expiresIn = payload.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var parsed) ? parsed : 0;
    var tokenType = payload.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "Bearer" : "Bearer";

    return new SocialTokenResult(
      true,
      "Token refreshed.",
      accessToken,
      refreshToken.Trim(),
      idToken,
      expiresIn,
      tokenType);
  }

  public async Task<(bool Success, string Message)> RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(refreshToken))
    {
      return (false, "Refresh token is required.");
    }

    var appClientId = _awsOptions.Cognito.AppClientId?.Trim();
    if (string.IsNullOrWhiteSpace(appClientId))
    {
      return (false, "Cognito AppClientId is not configured.");
    }

    var oauthBaseUrl = AuthenticationExtensions.ResolveCognitoOAuthBaseUrl(_awsOptions);
    var endpoint = new Uri($"{oauthBaseUrl.TrimEnd('/')}/oauth2/revoke");

    var form = new Dictionary<string, string>
    {
      ["token"] = refreshToken.Trim(),
      ["client_id"] = appClientId
    };

    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
      Content = new FormUrlEncodedContent(form)
    };

    var client = httpClientFactory.CreateClient(CognitoOAuthTokenClient.HttpClientName);
    using var response = await client.SendAsync(request, cancellationToken);

    return response.IsSuccessStatusCode
      ? (true, "Token revoked.")
      : (false, "Failed to revoke token.");
  }
}
