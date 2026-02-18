using Aarogya.Infrastructure.Aws;
using Aarogya.Infrastructure.Caching;
using Aarogya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aarogya.Infrastructure;

public static class DependencyInjection
{
  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "DbContext lifetime is managed by the DI container")]
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var connectionString = configuration.GetConnectionString("DefaultConnection")
      ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. "
        + "Set via user-secrets, environment variables (AAROGYA_ConnectionStrings__DefaultConnection), or appsettings.");

    var dbSection = configuration.GetSection("Database");
    var commandTimeout = dbSection.GetSection("CommandTimeoutSeconds").Get<int?>() ?? 30;
    var enableDetailedErrors = dbSection.GetSection("EnableDetailedErrors").Get<bool?>() ?? false;
    var enableSensitiveDataLogging = dbSection.GetSection("EnableSensitiveDataLogging").Get<bool?>() ?? false;
    var maxRetryCount = dbSection.GetSection("MaxRetryCount").Get<int?>() ?? 3;
    var maxRetryDelaySeconds = dbSection.GetSection("MaxRetryDelaySeconds").Get<int?>() ?? 5;
    var redisSection = configuration.GetSection("Redis");
    var redisConnectionString = configuration.GetConnectionString("Redis");
    var redisInstanceName = redisSection.GetSection("InstanceName").Get<string>() ?? "aarogya_";
    var redisDatabase = redisSection.GetSection("Database").Get<int?>() ?? 0;
    var redisConnectTimeout = redisSection.GetSection("ConnectTimeoutMilliseconds").Get<int?>() ?? 5000;
    var redisConnectRetry = redisSection.GetSection("ConnectRetry").Get<int?>() ?? 3;
    var redisSyncTimeout = redisSection.GetSection("SyncTimeoutMilliseconds").Get<int?>() ?? 5000;

    services.AddDbContextPool<AarogyaDbContext>(options =>
    {
      options.UseNpgsql(connectionString, npgsqlOptions =>
      {
        npgsqlOptions.CommandTimeout(commandTimeout);

        npgsqlOptions.MigrationsAssembly(typeof(AarogyaDbContext).Assembly.FullName);

        // Configure retry policy for transient failures
        npgsqlOptions.EnableRetryOnFailure(
          maxRetryCount: maxRetryCount,
          maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
          errorCodesToAdd: null);
      });

      if (enableDetailedErrors)
      {
        options.EnableDetailedErrors();
      }

      if (enableSensitiveDataLogging)
      {
        options.EnableSensitiveDataLogging();
      }
    });

    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
      services.AddStackExchangeRedisCache(options =>
      {
        options.Configuration = redisConnectionString;
        options.InstanceName = redisInstanceName;

        var redisConfiguration = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
        redisConfiguration.ConnectTimeout = redisConnectTimeout;
        redisConfiguration.ConnectRetry = redisConnectRetry;
        redisConfiguration.SyncTimeout = redisSyncTimeout;
        redisConfiguration.DefaultDatabase = redisDatabase;
        redisConfiguration.AbortOnConnectFail = false;
        options.ConfigurationOptions = redisConfiguration;
      });
    }

    // Register a health check for PostgreSQL
    var healthChecks = services.AddHealthChecks()
      .AddDbContextCheck<AarogyaDbContext>("postgresql", tags: ["db", "ready"]);

    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
      healthChecks.AddCheck<RedisDistributedCacheHealthCheck>("redis", tags: ["cache", "ready"]);
    }

    // Register AWS services (S3, SES)
    services.AddAwsServices(configuration);

    return services;
  }
}
