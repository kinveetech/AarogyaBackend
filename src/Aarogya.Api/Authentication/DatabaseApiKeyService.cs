using System.Security.Cryptography;
using Aarogya.Api.Configuration;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class DatabaseApiKeyService(
  IApiKeyRepository apiKeyRepository,
  IUnitOfWork unitOfWork,
  IOptions<ApiKeyOptions> options,
  IUtcClock clock,
  ApiKeyRateLimiter rateLimiter)
  : IApiKeyService
{
  private readonly ApiKeyOptions _options = options.Value;

  public async Task<ApiKeyIssueResult> IssueKeyAsync(ApiKeyIssueRequest request, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(request.PartnerId))
    {
      return new ApiKeyIssueResult(false, "Partner ID is required.");
    }

    if (string.IsNullOrWhiteSpace(request.PartnerName))
    {
      return new ApiKeyIssueResult(false, "Partner name is required.");
    }

    var now = clock.UtcNow;
    var plainKey = BuildApiKey();
    var keyHash = ComputeHash(plainKey);
    var keyPrefix = plainKey[..Math.Min(plainKey.Length, 12)];
    var expiresAt = now.AddDays(_options.DefaultKeyLifetimeDays);

    var entity = new ApiKey
    {
      Id = Guid.NewGuid(),
      KeyHash = keyHash,
      KeyPrefix = keyPrefix,
      PartnerId = request.PartnerId.Trim(),
      PartnerName = request.PartnerName.Trim(),
      ExpiresAt = expiresAt
    };

    await apiKeyRepository.AddAsync(entity, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return new ApiKeyIssueResult(
      true,
      "API key issued.",
      entity.Id.ToString("N"),
      plainKey,
      expiresAt,
      entity.PartnerId,
      entity.PartnerName);
  }

  public async Task<ApiKeyRotateResult> RotateKeyAsync(ApiKeyRotateRequest request, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(request.KeyId))
    {
      return new ApiKeyRotateResult(false, "Key ID is required.");
    }

    if (!Guid.TryParse(request.KeyId.Trim(), out var keyId))
    {
      return new ApiKeyRotateResult(false, "Invalid key ID format.");
    }

    var now = clock.UtcNow;
    var spec = new ActiveApiKeyByIdSpecification(keyId, now);
    var existing = await apiKeyRepository.FirstOrDefaultAsync(spec, cancellationToken);
    if (existing is null)
    {
      return new ApiKeyRotateResult(false, "Active API key not found.");
    }

    var overlapUntil = now.AddMinutes(_options.RotationOverlapMinutes);
    existing.ExpiresAt = overlapUntil;
    existing.OverlapExpiresAt = overlapUntil;
    apiKeyRepository.Update(existing);

    var newPlainKey = BuildApiKey();
    var newKeyHash = ComputeHash(newPlainKey);
    var newKeyPrefix = newPlainKey[..Math.Min(newPlainKey.Length, 12)];
    var newExpiresAt = now.AddDays(_options.DefaultKeyLifetimeDays);

    var newEntity = new ApiKey
    {
      Id = Guid.NewGuid(),
      KeyHash = newKeyHash,
      KeyPrefix = newKeyPrefix,
      PartnerId = existing.PartnerId,
      PartnerName = existing.PartnerName,
      ExpiresAt = newExpiresAt
    };

    await apiKeyRepository.AddAsync(newEntity, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return new ApiKeyRotateResult(
      true,
      "API key rotated.",
      newEntity.Id.ToString("N"),
      newPlainKey,
      newExpiresAt,
      overlapUntil,
      existing.PartnerId,
      existing.PartnerName);
  }

  public async Task<ApiKeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      return new ApiKeyValidationResult(false, "API key is required.");
    }

    var hash = ComputeHash(apiKey.Trim());
    var spec = new ApiKeyByHashSpecification(hash);
    var record = await apiKeyRepository.FirstOrDefaultAsync(spec, cancellationToken);
    if (record is null)
    {
      return new ApiKeyValidationResult(false, "Invalid API key.");
    }

    var now = clock.UtcNow;
    if (record.IsRevoked || record.ExpiresAt <= now)
    {
      return new ApiKeyValidationResult(false, "API key is revoked or expired.");
    }

    if (rateLimiter.IsRateLimited(record.Id.ToString("N")))
    {
      return new ApiKeyValidationResult(
        false,
        "Rate limit exceeded for API key.",
        record.Id.ToString("N"),
        record.PartnerId,
        record.PartnerName,
        true);
    }

    return new ApiKeyValidationResult(
      true,
      "API key valid.",
      record.Id.ToString("N"),
      record.PartnerId,
      record.PartnerName);
  }

  private string BuildApiKey()
  {
    var token = JwtTokenHelpers.GenerateToken(32);
    return string.Concat(_options.KeyPrefix.Trim(), token);
  }

  private static string ComputeHash(string input)
  {
    var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(bytes);
  }
}
