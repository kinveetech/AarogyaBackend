using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aarogya.Infrastructure.Persistence;

internal sealed class PostgreSqlConnectionHealthCheck(IServiceScopeFactory serviceScopeFactory) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(context);

    try
    {
      using var scope = serviceScopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
      var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

      return canConnect
        ? HealthCheckResult.Healthy("PostgreSQL connectivity check succeeded.")
        : HealthCheckResult.Unhealthy("PostgreSQL connectivity check failed.");
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      return HealthCheckResult.Unhealthy("PostgreSQL connectivity check timed out.");
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy("PostgreSQL connectivity check failed.", ex);
    }
  }
}
