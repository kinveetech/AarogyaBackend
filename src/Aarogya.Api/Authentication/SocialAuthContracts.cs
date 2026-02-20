using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Authentication;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API controller constructor injection.")]
public interface ISocialAuthService
{
  public Task<SocialAuthorizeResult> CreateAuthorizeUrlAsync(
    SocialAuthorizeRequest request,
    CancellationToken cancellationToken = default);

  public Task<SocialTokenResult> ExchangeCodeAsync(
    SocialTokenRequest request,
    CancellationToken cancellationToken = default);
}

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public service contract.")]
public sealed record SocialAuthorizeRequest(
  string Provider,
  Uri RedirectUri,
  string? State,
  string? CodeChallenge,
  string? CodeChallengeMethod);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by public service contract.")]
public sealed record SocialAuthorizeResult(
  bool Success,
  string Message,
  Uri? AuthorizeUrl = null,
  string? State = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public service contract.")]
public sealed record SocialTokenRequest(
  string Provider,
  Uri RedirectUri,
  string AuthorizationCode,
  string ProviderSubject,
  string Email,
  string? GivenName,
  string? FamilyName);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by public service contract.")]
public sealed record SocialTokenResult(
  bool Success,
  string Message,
  string? AccessToken = null,
  string? RefreshToken = null,
  string? IdToken = null,
  int ExpiresInSeconds = 0,
  string TokenType = "Bearer",
  bool IsLinkedAccount = false);
