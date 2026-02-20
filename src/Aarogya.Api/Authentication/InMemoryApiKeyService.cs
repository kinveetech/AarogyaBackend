using System.Collections.Concurrent;
using System.Security.Cryptography;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Authentication;

internal sealed class InMemoryApiKeyService(
  IOptions<ApiKeyOptions> options,
  IUtcClock clock)
  : IApiKeyService
{
  private readonly ApiKeyOptions _options = options.Value;
  private readonly ConcurrentDictionary<string, ApiKeyRecord> _keysByHash = new(StringComparer.Ordinal);
  private readonly ConcurrentDictionary<string, ApiKeyRateState> _rateByKeyId = new(StringComparer.Ordinal);

  public Task<ApiKeyIssueResult> IssueKeyAsync(ApiKeyIssueRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (string.IsNullOrWhiteSpace(request.PartnerId))
    {
      return Task.FromResult(new ApiKeyIssueResult(false, "Partner ID is required."));
    }

    if (string.IsNullOrWhiteSpace(request.PartnerName))
    {
      return Task.FromResult(new ApiKeyIssueResult(false, "Partner name is required."));
    }

    var now = clock.UtcNow;
    var keyId = Guid.NewGuid().ToString("N");
    var apiKey = BuildApiKey();
    var hash = ComputeHash(apiKey);
    var expiresAt = now.AddDays(_options.DefaultKeyLifetimeDays);

    _keysByHash[hash] = new ApiKeyRecord
    {
      KeyId = keyId,
      PartnerId = request.PartnerId.Trim(),
      PartnerName = request.PartnerName.Trim(),
      ExpiresAt = expiresAt,
      Revoked = false
    };

    _rateByKeyId.TryAdd(keyId, new ApiKeyRateState());

    return Task.FromResult(new ApiKeyIssueResult(
      true,
      "API key issued.",
      keyId,
      apiKey,
      expiresAt,
      request.PartnerId.Trim(),
      request.PartnerName.Trim()));
  }

  public Task<ApiKeyRotateResult> RotateKeyAsync(ApiKeyRotateRequest request, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (string.IsNullOrWhiteSpace(request.KeyId))
    {
      return Task.FromResult(new ApiKeyRotateResult(false, "Key ID is required."));
    }

    var now = clock.UtcNow;
    var existing = _keysByHash.Values.FirstOrDefault(record =>
      string.Equals(record.KeyId, request.KeyId.Trim(), StringComparison.Ordinal)
      && !record.Revoked
      && record.ExpiresAt > now);

    if (existing is null)
    {
      return Task.FromResult(new ApiKeyRotateResult(false, "Active API key not found."));
    }

    lock (existing.Sync)
    {
      var overlapUntil = now.AddMinutes(_options.RotationOverlapMinutes);
      if (existing.ExpiresAt < overlapUntil)
      {
        existing.ExpiresAt = overlapUntil;
      }

      var newKeyId = Guid.NewGuid().ToString("N");
      var newApiKey = BuildApiKey();
      var newHash = ComputeHash(newApiKey);
      var newExpiresAt = now.AddDays(_options.DefaultKeyLifetimeDays);

      _keysByHash[newHash] = new ApiKeyRecord
      {
        KeyId = newKeyId,
        PartnerId = existing.PartnerId,
        PartnerName = existing.PartnerName,
        ExpiresAt = newExpiresAt,
        Revoked = false
      };

      _rateByKeyId.TryAdd(newKeyId, new ApiKeyRateState());

      return Task.FromResult(new ApiKeyRotateResult(
        true,
        "API key rotated.",
        newKeyId,
        newApiKey,
        newExpiresAt,
        existing.ExpiresAt,
        existing.PartnerId,
        existing.PartnerName));
    }
  }

  public Task<ApiKeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (string.IsNullOrWhiteSpace(apiKey))
    {
      return Task.FromResult(new ApiKeyValidationResult(false, "API key is required."));
    }

    var hash = ComputeHash(apiKey.Trim());
    if (!_keysByHash.TryGetValue(hash, out var record))
    {
      return Task.FromResult(new ApiKeyValidationResult(false, "Invalid API key."));
    }

    var now = clock.UtcNow;
    if (record.Revoked || record.ExpiresAt <= now)
    {
      return Task.FromResult(new ApiKeyValidationResult(false, "API key is revoked or expired."));
    }

    var state = _rateByKeyId.GetOrAdd(record.KeyId, _ => new ApiKeyRateState());
    lock (state.Sync)
    {
      var windowStart = now.AddSeconds(-_options.RateLimitWindowSeconds);
      state.Requests = state.Requests
        .Where(timestamp => timestamp >= windowStart)
        .ToList();

      if (state.Requests.Count >= _options.MaxRequestsPerWindow)
      {
        return Task.FromResult(new ApiKeyValidationResult(
          false,
          "Rate limit exceeded for API key.",
          record.KeyId,
          record.PartnerId,
          record.PartnerName,
          true));
      }

      state.Requests.Add(now);
    }

    return Task.FromResult(new ApiKeyValidationResult(
      true,
      "API key valid.",
      record.KeyId,
      record.PartnerId,
      record.PartnerName));
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

  private sealed class ApiKeyRecord
  {
    public object Sync { get; } = new();

    public string KeyId { get; set; } = string.Empty;

    public string PartnerId { get; set; } = string.Empty;

    public string PartnerName { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public bool Revoked { get; set; }
  }

  private sealed class ApiKeyRateState
  {
    public object Sync { get; } = new();

    public List<DateTimeOffset> Requests { get; set; } = [];
  }
}
