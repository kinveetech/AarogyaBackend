using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Authentication;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API controller constructor injection.")]
public interface IApiKeyService
{
  public Task<ApiKeyIssueResult> IssueKeyAsync(ApiKeyIssueRequest request, CancellationToken cancellationToken = default);

  public Task<ApiKeyRotateResult> RotateKeyAsync(ApiKeyRotateRequest request, CancellationToken cancellationToken = default);

  public Task<ApiKeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken = default);
}

[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "Used by public service contract.")]
public sealed record ApiKeyIssueRequest(string PartnerId, string PartnerName);

[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "Used by public service contract.")]
public sealed record ApiKeyRotateRequest(string KeyId);

[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "Returned by public service contract.")]
public sealed record ApiKeyIssueResult(
  bool Success,
  string Message,
  string? KeyId = null,
  string? ApiKey = null,
  DateTimeOffset? ExpiresAt = null,
  string? PartnerId = null,
  string? PartnerName = null);

[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "Returned by public service contract.")]
public sealed record ApiKeyRotateResult(
  bool Success,
  string Message,
  string? KeyId = null,
  string? ApiKey = null,
  DateTimeOffset? ExpiresAt = null,
  DateTimeOffset? PreviousKeyValidUntil = null,
  string? PartnerId = null,
  string? PartnerName = null);

[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "Returned by public service contract.")]
public sealed record ApiKeyValidationResult(
  bool Success,
  string Message,
  string? KeyId = null,
  string? PartnerId = null,
  string? PartnerName = null,
  bool IsRateLimited = false);
