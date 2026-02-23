using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class CognitoSocialAuthService(
  IOptions<AwsOptions> awsOptions,
  ICognitoSocialTokenClient cognitoSocialTokenClient)
  : ISocialAuthService
{
  private readonly AwsOptions _awsOptions = awsOptions.Value;

  public Task<SocialAuthorizeResult> CreateAuthorizeUrlAsync(
    SocialAuthorizeRequest request,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var provider = NormalizeProvider(request.Provider);
    if (provider is null)
    {
      return Task.FromResult(new SocialAuthorizeResult(false, "Unsupported provider."));
    }

    if (!TryGetProviderOptions(provider, out var providerOptions) || !providerOptions.Enabled)
    {
      return Task.FromResult(new SocialAuthorizeResult(false, $"Provider '{provider}' is not enabled."));
    }

    if (!IsAllowedRedirectUri(request.RedirectUri))
    {
      return Task.FromResult(new SocialAuthorizeResult(false, "Redirect URI is not in the allowed list."));
    }

    var state = string.IsNullOrWhiteSpace(request.State) ? JwtTokenHelpers.GenerateToken(16) : request.State.Trim();
    var clientId = _awsOptions.Cognito.AppClientId?.Trim();
    if (string.IsNullOrWhiteSpace(clientId))
    {
      return Task.FromResult(new SocialAuthorizeResult(false, "Cognito AppClientId is not configured."));
    }

    var oauthBaseUrl = AuthenticationExtensions.ResolveCognitoOAuthBaseUrl(_awsOptions);
    var authorizeUrl = BuildAuthorizeUrl(
      oauthBaseUrl,
      clientId,
      provider,
      request.RedirectUri.ToString(),
      state,
      providerOptions.Scopes,
      request.CodeChallenge,
      request.CodeChallengeMethod);

    return Task.FromResult(new SocialAuthorizeResult(true, "Authorize URL generated.", new Uri(authorizeUrl), state));
  }

  public Task<SocialTokenResult> ExchangeCodeAsync(
    SocialTokenRequest request,
    CancellationToken cancellationToken = default)
  {
    return ExchangeCodeInternalAsync(request, cancellationToken);
  }

  private async Task<SocialTokenResult> ExchangeCodeInternalAsync(
    SocialTokenRequest request,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var provider = NormalizeProvider(request.Provider);
    if (provider is null)
    {
      return new SocialTokenResult(false, "Unsupported provider.");
    }

    if (!TryGetProviderOptions(provider, out var providerOptions) || !providerOptions.Enabled)
    {
      return new SocialTokenResult(false, $"Provider '{provider}' is not enabled.");
    }

    if (!IsAllowedRedirectUri(request.RedirectUri))
    {
      return new SocialTokenResult(false, "Redirect URI is not in the allowed list.");
    }

    if (string.IsNullOrWhiteSpace(request.AuthorizationCode))
    {
      return new SocialTokenResult(false, "Authorization code is required.");
    }

    var exchange = await cognitoSocialTokenClient.ExchangeAuthorizationCodeAsync(
      provider,
      request.RedirectUri,
      request.AuthorizationCode,
      request.CodeVerifier,
      cancellationToken);

    if (!exchange.Success || exchange.Identity is null)
    {
      return new SocialTokenResult(false, exchange.Message);
    }

    return new SocialTokenResult(
      true,
      "Social login successful.",
      exchange.AccessToken,
      exchange.RefreshToken,
      exchange.IdToken,
      exchange.ExpiresInSeconds,
      exchange.TokenType,
      false);
  }

  private bool IsAllowedRedirectUri(Uri redirectUri)
  {
    if (!redirectUri.IsAbsoluteUri)
    {
      return false;
    }

    return _awsOptions.Cognito.SocialIdentityProviders.AllowedRedirectUris
      .Exists(uri => string.Equals(uri?.Trim(), redirectUri.ToString(), StringComparison.OrdinalIgnoreCase));
  }

  private bool TryGetProviderOptions(string provider, out SocialProviderOptions providerOptions)
  {
    providerOptions = provider switch
    {
      "Google" => _awsOptions.Cognito.SocialIdentityProviders.Google,
      "Apple" => _awsOptions.Cognito.SocialIdentityProviders.Apple,
      "Facebook" => _awsOptions.Cognito.SocialIdentityProviders.Facebook,
      _ => new SocialProviderOptions()
    };

    return provider is "Google" or "Apple" or "Facebook";
  }

  private static string? NormalizeProvider(string? provider)
  {
    if (string.IsNullOrWhiteSpace(provider))
    {
      return null;
    }

    return provider.Trim().ToUpperInvariant() switch
    {
      "GOOGLE" => "Google",
      "APPLE" => "Apple",
      "FACEBOOK" => "Facebook",
      _ => null
    };
  }

  private static string BuildAuthorizeUrl(
    string oauthBaseUrl,
    string clientId,
    string provider,
    string redirectUri,
    string state,
    IReadOnlyList<string> scopes,
    string? codeChallenge,
    string? codeChallengeMethod)
  {
    var scope = string.Join(' ', scopes.Where(static scopeValue => !string.IsNullOrWhiteSpace(scopeValue)));
    var query = new Dictionary<string, string?>
    {
      ["response_type"] = "code",
      ["client_id"] = clientId,
      ["redirect_uri"] = redirectUri,
      ["identity_provider"] = provider,
      ["scope"] = string.IsNullOrWhiteSpace(scope) ? "openid email profile" : scope,
      ["state"] = state
    };

    if (!string.IsNullOrWhiteSpace(codeChallenge))
    {
      query["code_challenge"] = codeChallenge.Trim();
      query["code_challenge_method"] = string.IsNullOrWhiteSpace(codeChallengeMethod) ? "S256" : codeChallengeMethod.Trim();
    }

    var queryString = string.Join('&', query.Select(static kvp =>
      $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? string.Empty)}"));

    return $"{oauthBaseUrl.TrimEnd('/')}/oauth2/authorize?{queryString}";
  }
}
