using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public service constructor injection and test doubles.")]
public interface ICognitoSocialTokenClient
{
  public Task<CognitoSocialTokenExchangeResult> ExchangeAuthorizationCodeAsync(
    string provider,
    Uri redirectUri,
    string authorizationCode,
    string? codeVerifier,
    CancellationToken cancellationToken = default);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by public service contract.")]
public sealed record SocialIdentityClaims(
  string ProviderSubject,
  string Email,
  string? GivenName,
  string? FamilyName);

[System.Diagnostics.CodeAnalysis.SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by public service contract.")]
public sealed record CognitoSocialTokenExchangeResult(
  bool Success,
  string Message,
  SocialIdentityClaims? Identity = null,
  int ExpiresInSeconds = 0,
  string TokenType = "Bearer");

internal sealed class CognitoOAuthTokenClient(
  IOptions<AwsOptions> awsOptions,
  IHttpClientFactory httpClientFactory)
  : ICognitoSocialTokenClient
{
  internal const string HttpClientName = "CognitoOAuth";
  private readonly AwsOptions _awsOptions = awsOptions.Value;

  public async Task<CognitoSocialTokenExchangeResult> ExchangeAuthorizationCodeAsync(
    string provider,
    Uri redirectUri,
    string authorizationCode,
    string? codeVerifier,
    CancellationToken cancellationToken = default)
  {
    var appClientId = _awsOptions.Cognito.AppClientId?.Trim();
    if (string.IsNullOrWhiteSpace(appClientId))
    {
      return new CognitoSocialTokenExchangeResult(false, "Cognito AppClientId is not configured.");
    }

    var issuer = AuthenticationExtensions.ResolveCognitoIssuer(_awsOptions);
    var endpoint = new Uri($"{issuer.TrimEnd('/')}/oauth2/token");

    var form = new Dictionary<string, string>
    {
      ["grant_type"] = "authorization_code",
      ["client_id"] = appClientId,
      ["code"] = authorizationCode.Trim(),
      ["redirect_uri"] = redirectUri.ToString()
    };

    if (!string.IsNullOrWhiteSpace(codeVerifier))
    {
      form["code_verifier"] = codeVerifier.Trim();
    }

    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
      Content = new FormUrlEncodedContent(form)
    };

    var client = httpClientFactory.CreateClient(HttpClientName);
    using var response = await client.SendAsync(request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      return new CognitoSocialTokenExchangeResult(
        false,
        $"Failed to exchange social authorization code for provider '{provider}'.");
    }

    var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
    if (payload.ValueKind != JsonValueKind.Object)
    {
      return new CognitoSocialTokenExchangeResult(false, "Token response payload is invalid.");
    }

    if (!payload.TryGetProperty("id_token", out var idTokenJson)
      || string.IsNullOrWhiteSpace(idTokenJson.GetString()))
    {
      return new CognitoSocialTokenExchangeResult(false, "Token response is missing id_token.");
    }

    var idTokenRaw = idTokenJson.GetString()!;
    var jwtHandler = new JwtSecurityTokenHandler();
    if (!jwtHandler.CanReadToken(idTokenRaw))
    {
      return new CognitoSocialTokenExchangeResult(false, "Unable to parse id_token returned by Cognito.");
    }

    JwtSecurityToken idToken;
    try
    {
      idToken = jwtHandler.ReadJwtToken(idTokenRaw);
    }
    catch (ArgumentException)
    {
      return new CognitoSocialTokenExchangeResult(false, "Unable to parse id_token returned by Cognito.");
    }

    if (!string.Equals(idToken.Issuer.TrimEnd('/'), issuer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
    {
      return new CognitoSocialTokenExchangeResult(false, "Token issuer mismatch for Cognito social exchange.");
    }

    if (!idToken.Audiences.Contains(appClientId, StringComparer.Ordinal))
    {
      return new CognitoSocialTokenExchangeResult(false, "Token audience mismatch for Cognito social exchange.");
    }

    var providerSubject = idToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub)?.Value;
    var email = idToken.Claims.FirstOrDefault(claim => claim.Type is JwtRegisteredClaimNames.Email or "email")?.Value;
    if (string.IsNullOrWhiteSpace(providerSubject) || string.IsNullOrWhiteSpace(email))
    {
      return new CognitoSocialTokenExchangeResult(false, "Token response is missing required user claims.");
    }

    var identity = new SocialIdentityClaims(
      providerSubject.Trim(),
      email.Trim(),
      idToken.Claims.FirstOrDefault(claim => claim.Type == "given_name")?.Value,
      idToken.Claims.FirstOrDefault(claim => claim.Type == "family_name")?.Value);

    var expiresIn = payload.TryGetProperty("expires_in", out var expiresInJson)
      && expiresInJson.ValueKind == JsonValueKind.Number
      && expiresInJson.TryGetInt32(out var parsedExpires)
      ? parsedExpires
      : 0;

    var tokenType = payload.TryGetProperty("token_type", out var tokenTypeJson)
      ? tokenTypeJson.GetString() ?? "Bearer"
      : "Bearer";

    return new CognitoSocialTokenExchangeResult(
      true,
      "Cognito social token exchange successful.",
      identity,
      expiresIn,
      tokenType);
  }
}
