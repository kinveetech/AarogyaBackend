using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Aarogya.Api.Authorization;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class InMemorySocialAuthService(
  IOptions<AwsOptions> awsOptions,
  IOptions<PkceOptions> pkceOptions,
  IOptions<JwtOptions> jwtOptions,
  IUtcClock clock)
  : ISocialAuthService
{
  private readonly AwsOptions _awsOptions = awsOptions.Value;
  private readonly PkceOptions _pkceOptions = pkceOptions.Value;
  private readonly JwtOptions _jwtOptions = jwtOptions.Value;
  private readonly ConcurrentDictionary<string, string> _subjectsByEmail = new(StringComparer.OrdinalIgnoreCase);
  private readonly ConcurrentDictionary<string, string> _subjectByProviderIdentity = new(StringComparer.OrdinalIgnoreCase);
  private readonly ConcurrentDictionary<string, string> _providerBySubject = new(StringComparer.OrdinalIgnoreCase);

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

    if (!IsAllowedMobileRedirectUri(request.RedirectUri))
    {
      return Task.FromResult(new SocialAuthorizeResult(false, "Redirect URI is not allowed for mobile OAuth flow."));
    }

    var state = string.IsNullOrWhiteSpace(request.State) ? JwtTokenHelpers.GenerateToken(16) : request.State.Trim();
    var issuer = AuthenticationExtensions.ResolveCognitoIssuer(_awsOptions);
    var clientId = _awsOptions.Cognito.AppClientId?.Trim();
    if (string.IsNullOrWhiteSpace(clientId))
    {
      return Task.FromResult(new SocialAuthorizeResult(false, "Cognito AppClientId is not configured."));
    }

    var authorizeUrl = BuildAuthorizeUrl(
      issuer,
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
    cancellationToken.ThrowIfCancellationRequested();

    var provider = NormalizeProvider(request.Provider);
    if (provider is null)
    {
      return Task.FromResult(new SocialTokenResult(false, "Unsupported provider."));
    }

    if (!TryGetProviderOptions(provider, out var providerOptions) || !providerOptions.Enabled)
    {
      return Task.FromResult(new SocialTokenResult(false, $"Provider '{provider}' is not enabled."));
    }

    if (!IsAllowedMobileRedirectUri(request.RedirectUri))
    {
      return Task.FromResult(new SocialTokenResult(false, "Redirect URI is not allowed for mobile OAuth flow."));
    }

    if (string.IsNullOrWhiteSpace(request.AuthorizationCode))
    {
      return Task.FromResult(new SocialTokenResult(false, "Authorization code is required."));
    }

    if (string.IsNullOrWhiteSpace(request.ProviderSubject))
    {
      return Task.FromResult(new SocialTokenResult(false, "Provider subject is required."));
    }

    if (!JwtTokenHelpers.CanIssueJwtTokens(_jwtOptions))
    {
      return Task.FromResult(new SocialTokenResult(false, "JWT issuer is not configured."));
    }

    var mappedEmail = request.Email.Trim();
    if (string.IsNullOrWhiteSpace(mappedEmail))
    {
      return Task.FromResult(new SocialTokenResult(false, "Email is required."));
    }

    var providerIdentityKey = $"{provider}:{request.ProviderSubject.Trim()}";
    var existingSubject = _subjectByProviderIdentity.GetValueOrDefault(providerIdentityKey);
    var subjectByEmail = _subjectsByEmail.GetValueOrDefault(mappedEmail);

    var subject = existingSubject
      ?? subjectByEmail
      ?? JwtTokenHelpers.GenerateToken(16);

    _subjectsByEmail[mappedEmail] = subject;
    _subjectByProviderIdentity[providerIdentityKey] = subject;
    _providerBySubject[subject] = provider;

    var isLinked = existingSubject is null && subjectByEmail is not null;

    var accessToken = GenerateJwtToken(subject, AarogyaRoles.Patient, mappedEmail, request.GivenName, request.FamilyName);
    var idToken = GenerateJwtToken(subject, AarogyaRoles.Patient, mappedEmail, request.GivenName, request.FamilyName);
    var refreshToken = JwtTokenHelpers.GenerateToken(48);

    return Task.FromResult(new SocialTokenResult(
      true,
      "Social login successful.",
      accessToken,
      refreshToken,
      idToken,
      _pkceOptions.AccessTokenExpirySeconds,
      "Bearer",
      isLinked));
  }

  private bool IsAllowedMobileRedirectUri(Uri redirectUri)
  {
    if (!redirectUri.IsAbsoluteUri)
    {
      return false;
    }

    return _awsOptions.Cognito.SocialIdentityProviders.MobileRedirectUris
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
    string issuer,
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

    return $"{issuer.TrimEnd('/')}/oauth2/authorize?{queryString}";
  }

  private string GenerateJwtToken(
    string subject,
    string role,
    string email,
    string? givenName,
    string? familyName)
  {
    var claims = new List<Claim>
    {
      new(JwtRegisteredClaimNames.Sub, subject),
      new(JwtRegisteredClaimNames.Email, email),
      new("email", email),
      new("given_name", givenName ?? string.Empty),
      new("family_name", familyName ?? string.Empty),
      new("cognito:groups", role),
      new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
    };

    return JwtTokenHelpers.GenerateJwtToken(clock, _jwtOptions, _pkceOptions.AccessTokenExpirySeconds, claims);
  }
}
