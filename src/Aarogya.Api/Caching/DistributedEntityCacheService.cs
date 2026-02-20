using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Aarogya.Api.Caching;

internal sealed class DistributedEntityCacheService(
  IDistributedCache distributedCache,
  ILogger<DistributedEntityCacheService> logger)
  : IEntityCacheService
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
  private static readonly TimeSpan VersionTtl = TimeSpan.FromDays(30);

  public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
  {
    string? payload;
    try
    {
      payload = await distributedCache.GetStringAsync(key, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Cache read failed for key {CacheKey}. Falling back to source of truth.", key);
      return default;
    }

    if (string.IsNullOrWhiteSpace(payload))
    {
      return default;
    }

    try
    {
      return JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }
    catch (JsonException ex)
    {
      logger.LogWarning(ex, "Failed to deserialize cache entry for key {CacheKey}. Removing corrupted value.", key);
      await distributedCache.RemoveAsync(key, cancellationToken);
      return default;
    }
  }

  public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
  {
    try
    {
      var payload = JsonSerializer.Serialize(value, SerializerOptions);
      var options = new DistributedCacheEntryOptions
      {
        AbsoluteExpirationRelativeToNow = ttl
      };

      await distributedCache.SetStringAsync(key, payload, options, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Cache write failed for key {CacheKey}.", key);
    }
  }

  public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
  {
    try
    {
      await distributedCache.RemoveAsync(key, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Cache remove failed for key {CacheKey}.", key);
    }
  }

  public async Task<string> GetNamespaceVersionAsync(string cacheNamespace, CancellationToken cancellationToken = default)
  {
    var key = ToNamespaceVersionKey(cacheNamespace);
    string? version;
    try
    {
      version = await distributedCache.GetStringAsync(key, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Cache namespace version lookup failed for namespace {CacheNamespace}.", cacheNamespace);
      return "fallback";
    }

    if (!string.IsNullOrWhiteSpace(version))
    {
      return version;
    }

    version = "1";
    try
    {
      await distributedCache.SetStringAsync(
        key,
        version,
        new DistributedCacheEntryOptions
        {
          AbsoluteExpirationRelativeToNow = VersionTtl
        },
        cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Cache namespace version initialization failed for namespace {CacheNamespace}.", cacheNamespace);
      return "fallback";
    }

    return version;
  }

  public async Task BumpNamespaceVersionAsync(string cacheNamespace, CancellationToken cancellationToken = default)
  {
    var key = ToNamespaceVersionKey(cacheNamespace);
    var version = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}");

    try
    {
      await distributedCache.SetStringAsync(
        key,
        version,
        new DistributedCacheEntryOptions
        {
          AbsoluteExpirationRelativeToNow = VersionTtl
        },
        cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Cache namespace version bump failed for namespace {CacheNamespace}.", cacheNamespace);
    }
  }

  private static string ToNamespaceVersionKey(string cacheNamespace)
    => $"cache:version:{cacheNamespace}";
}
