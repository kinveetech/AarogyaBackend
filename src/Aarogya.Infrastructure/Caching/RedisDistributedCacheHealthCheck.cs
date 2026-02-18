using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aarogya.Infrastructure.Caching;

public sealed class RedisDistributedCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    const string healthCheckKey = "__healthcheck:redis";

    try
    {
      var options = new DistributedCacheEntryOptions
      {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
      };

      await cache.SetStringAsync(healthCheckKey, "ok", options, cancellationToken);
      var value = await cache.GetStringAsync(healthCheckKey, cancellationToken);

      return value == "ok"
        ? HealthCheckResult.Healthy("Redis cache is reachable.")
        : HealthCheckResult.Unhealthy("Redis cache read/write validation failed.");
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy("Redis cache health check failed.", ex);
    }
  }
}
