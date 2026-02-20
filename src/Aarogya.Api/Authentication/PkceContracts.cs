using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Authentication;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API controller constructor injection.")]
public interface IPkceAuthorizationService
{
  public Task<PkceAuthorizeResult> CreateAuthorizationCodeAsync(
    PkceAuthorizeRequest request,
    CancellationToken cancellationToken = default);

  public Task<PkceTokenResult> ExchangeAuthorizationCodeAsync(
    PkceTokenRequest request,
    CancellationToken cancellationToken = default);
}

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by public service contract.")]
public sealed record PkceAuthorizeResult(
  bool Success,
  string Message,
  string? AuthorizationCode = null,
  DateTimeOffset? ExpiresAt = null,
  string? State = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by public service contract.")]
public sealed record PkceTokenResult(
  bool Success,
  string Message,
  string? AccessToken = null,
  string? RefreshToken = null,
  string? IdToken = null,
  int ExpiresInSeconds = 0,
  string TokenType = "Bearer");

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public service contract.")]
public sealed record PkceAuthorizeRequest(
  string ClientId,
  Uri RedirectUri,
  string CodeChallenge,
  string CodeChallengeMethod,
  string Platform,
  string? Scope,
  string? State);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public service contract.")]
public sealed record PkceTokenRequest(
  string ClientId,
  Uri RedirectUri,
  string AuthorizationCode,
  string CodeVerifier);
