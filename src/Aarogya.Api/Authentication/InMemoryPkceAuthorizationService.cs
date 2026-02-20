using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Aarogya.Api.Authentication;

internal sealed class InMemoryPkceAuthorizationService(
  IOptions<PkceOptions> pkceOptions,
  IOptions<AwsOptions> awsOptions,
  IOptions<JwtOptions> jwtOptions,
  IUtcClock clock)
  : IPkceAuthorizationService
{
  private static readonly Regex CodeVerifierRegex = new(
    @"^[A-Za-z0-9\-._~]{43,128}$",
    RegexOptions.Compiled | RegexOptions.CultureInvariant,
    TimeSpan.FromMilliseconds(200));

  private static readonly Regex CodeChallengeRegex = new(
    @"^[A-Za-z0-9\-_]{43}$",
    RegexOptions.Compiled | RegexOptions.CultureInvariant,
    TimeSpan.FromMilliseconds(200));

  private readonly PkceOptions _pkceOptions = pkceOptions.Value;
  private readonly AwsOptions _awsOptions = awsOptions.Value;
  private readonly JwtOptions _jwtOptions = jwtOptions.Value;
  private readonly ConcurrentDictionary<string, AuthorizationCodeEntry> _authorizationCodes = new(StringComparer.Ordinal);

  public Task<PkceAuthorizeResult> CreateAuthorizationCodeAsync(
    PkceAuthorizeRequest request,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    EvictExpiredAuthorizationCodes(clock.UtcNow);

    if (!ValidateAuthorizeRequest(request, out var validationMessage))
    {
      return Task.FromResult(new PkceAuthorizeResult(false, validationMessage));
    }

    var now = clock.UtcNow;
    var expiresAt = now.AddSeconds(_pkceOptions.AuthorizationCodeExpirySeconds);
    var authorizationCode = GenerateToken(32);
    var normalizedPlatform = request.Platform.Trim().ToUpperInvariant();

    _authorizationCodes[authorizationCode] = new AuthorizationCodeEntry
    {
      ClientId = request.ClientId.Trim(),
      RedirectUri = request.RedirectUri.AbsoluteUri,
      CodeChallenge = request.CodeChallenge.Trim(),
      CodeChallengeMethod = request.CodeChallengeMethod.Trim().ToUpperInvariant(),
      Platform = normalizedPlatform,
      Scope = request.Scope?.Trim(),
      ExpiresAt = expiresAt
    };

    return Task.FromResult(new PkceAuthorizeResult(
      true,
      "Authorization code created.",
      authorizationCode,
      expiresAt,
      request.State));
  }

  public Task<PkceTokenResult> ExchangeAuthorizationCodeAsync(
    PkceTokenRequest request,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var now = clock.UtcNow;
    EvictExpiredAuthorizationCodes(now);

    if (!ValidateTokenRequest(request, out var validationMessage))
    {
      return Task.FromResult(new PkceTokenResult(false, validationMessage));
    }

    if (!_authorizationCodes.TryGetValue(request.AuthorizationCode.Trim(), out var entry))
    {
      return Task.FromResult(new PkceTokenResult(false, "Invalid or expired authorization code."));
    }

    lock (entry.Lock)
    {
      if (entry.IsConsumed)
      {
        _authorizationCodes.TryRemove(request.AuthorizationCode.Trim(), out _);
        return Task.FromResult(new PkceTokenResult(false, "Authorization code already used."));
      }

      if (entry.ExpiresAt < now)
      {
        _authorizationCodes.TryRemove(request.AuthorizationCode.Trim(), out _);
        return Task.FromResult(new PkceTokenResult(false, "Authorization code expired."));
      }

      if (!string.Equals(entry.ClientId, request.ClientId.Trim(), StringComparison.Ordinal))
      {
        return Task.FromResult(new PkceTokenResult(false, "Client ID mismatch."));
      }

      if (!string.Equals(entry.RedirectUri, request.RedirectUri.AbsoluteUri, StringComparison.Ordinal))
      {
        return Task.FromResult(new PkceTokenResult(false, "Redirect URI mismatch."));
      }

      var computedChallenge = ComputeCodeChallenge(request.CodeVerifier.Trim());
      if (!string.Equals(computedChallenge, entry.CodeChallenge, StringComparison.Ordinal))
      {
        return Task.FromResult(new PkceTokenResult(false, "Invalid PKCE code verifier."));
      }

      entry.IsConsumed = true;
    }

    _authorizationCodes.TryRemove(request.AuthorizationCode.Trim(), out _);

    if (!CanIssueJwtTokens(_jwtOptions))
    {
      return Task.FromResult(new PkceTokenResult(false, "JWT issuer is not configured."));
    }

    var subject = GenerateToken(16);
    var accessToken = GenerateJwtToken(subject, isIdToken: false);
    var idToken = GenerateJwtToken(subject, isIdToken: true);
    var refreshToken = GenerateToken(48);

    return Task.FromResult(new PkceTokenResult(
      true,
      "Token exchange successful.",
      accessToken,
      refreshToken,
      idToken,
      _pkceOptions.AccessTokenExpirySeconds));
  }

  private bool ValidateAuthorizeRequest(PkceAuthorizeRequest request, out string validationMessage)
  {
    validationMessage = string.Empty;

    if (string.IsNullOrWhiteSpace(request.ClientId))
    {
      validationMessage = "Client ID is required.";
      return false;
    }

    var configuredClientId = _awsOptions.Cognito.AppClientId;
    if (!string.IsNullOrWhiteSpace(configuredClientId)
      && !configuredClientId.Contains("SET_VIA", StringComparison.OrdinalIgnoreCase)
      && !string.Equals(request.ClientId.Trim(), configuredClientId, StringComparison.Ordinal))
    {
      validationMessage = "Unknown client ID.";
      return false;
    }

    if (!request.RedirectUri.IsAbsoluteUri)
    {
      validationMessage = "Redirect URI must be absolute.";
      return false;
    }

    if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase))
    {
      validationMessage = "Only S256 code challenge method is supported.";
      return false;
    }

    if (string.IsNullOrWhiteSpace(request.CodeChallenge)
      || !CodeChallengeRegex.IsMatch(request.CodeChallenge.Trim()))
    {
      validationMessage = "Code challenge is invalid.";
      return false;
    }

    var platform = request.Platform?.Trim().ToUpperInvariant();
    if (platform is not ("IOS" or "ANDROID"))
    {
      validationMessage = "Platform must be ios or android.";
      return false;
    }

    return true;
  }

  private static bool ValidateTokenRequest(PkceTokenRequest request, out string validationMessage)
  {
    validationMessage = string.Empty;

    if (string.IsNullOrWhiteSpace(request.ClientId))
    {
      validationMessage = "Client ID is required.";
      return false;
    }

    if (!request.RedirectUri.IsAbsoluteUri)
    {
      validationMessage = "Redirect URI must be absolute.";
      return false;
    }

    if (string.IsNullOrWhiteSpace(request.AuthorizationCode))
    {
      validationMessage = "Authorization code is required.";
      return false;
    }

    if (string.IsNullOrWhiteSpace(request.CodeVerifier)
      || !CodeVerifierRegex.IsMatch(request.CodeVerifier.Trim()))
    {
      validationMessage = "Code verifier is invalid.";
      return false;
    }

    return true;
  }

  private static string ComputeCodeChallenge(string codeVerifier)
  {
    var verifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
    var hash = SHA256.HashData(verifierBytes);
    return Base64UrlEncode(hash);
  }

  private static string GenerateToken(int bytesLength)
  {
    var bytes = RandomNumberGenerator.GetBytes(bytesLength);
    return Base64UrlEncode(bytes);
  }

  private static string Base64UrlEncode(byte[] bytes)
  {
    return Convert.ToBase64String(bytes)
      .TrimEnd('=')
      .Replace('+', '-')
      .Replace('/', '_');
  }

  private void EvictExpiredAuthorizationCodes(DateTimeOffset now)
  {
    foreach (var kvp in _authorizationCodes.Where(kvp => kvp.Value.ExpiresAt < now || kvp.Value.IsConsumed))
    {
      _authorizationCodes.TryRemove(kvp.Key, out _);
    }
  }

  private string GenerateJwtToken(string subject, bool isIdToken)
  {
    var now = clock.UtcNow;
    var expiresAt = now.AddSeconds(_pkceOptions.AccessTokenExpirySeconds);
    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
    var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    var claims = new List<Claim>
    {
      new(JwtRegisteredClaimNames.Sub, subject),
      new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
      new("token_use", isIdToken ? "id" : "access")
    };

    var descriptor = new SecurityTokenDescriptor
    {
      Issuer = _jwtOptions.Issuer,
      Audience = _jwtOptions.Audience,
      Subject = new ClaimsIdentity(claims),
      NotBefore = now.UtcDateTime,
      Expires = expiresAt.UtcDateTime,
      SigningCredentials = signingCredentials
    };

    var handler = new JwtSecurityTokenHandler();
    var token = handler.CreateToken(descriptor);
    return handler.WriteToken(token);
  }

  private static bool CanIssueJwtTokens(JwtOptions jwtOptions)
  {
    if (string.IsNullOrWhiteSpace(jwtOptions.Key))
    {
      return false;
    }

    if (jwtOptions.Key.Contains("SET_VIA", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    return jwtOptions.Key.Length >= 32
      && !string.IsNullOrWhiteSpace(jwtOptions.Issuer)
      && !string.IsNullOrWhiteSpace(jwtOptions.Audience);
  }

  private sealed class AuthorizationCodeEntry
  {
    public object Lock { get; } = new();

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string CodeChallenge { get; set; } = string.Empty;

    public string CodeChallengeMethod { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string? Scope { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsConsumed { get; set; }
  }
}
