using Aarogya.Domain.Repositories;
using Aarogya.Infrastructure.Aadhaar;
using Aarogya.Infrastructure.Aws;
using Aarogya.Infrastructure.Caching;
using Aarogya.Infrastructure.Persistence;
using Aarogya.Infrastructure.Persistence.Repositories;
using Aarogya.Infrastructure.Security;
using Aarogya.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

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
    var healthCheckTimeoutSeconds = dbSection.GetSection("HealthCheckTimeoutSeconds").Get<int?>() ?? 5;
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

    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<IAadhaarVaultRepository, AadhaarVaultRepository>();
    services.AddScoped<IReportRepository, ReportRepository>();
    services.AddScoped<IAccessGrantRepository, AccessGrantRepository>();
    services.AddScoped<IEmergencyContactRepository, EmergencyContactRepository>();
    services.AddScoped<IAuditLogRepository, AuditLogRepository>();
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    var seedDataOptions = new SeedDataOptions();
    configuration.GetSection(SeedDataOptions.SectionName).Bind(seedDataOptions);
    services.AddSingleton(Options.Create(seedDataOptions));
    services.AddScoped<IDataSeeder, DevelopmentDataSeeder>();
    var aadhaarVaultOptions = new AadhaarVaultOptions();
    configuration.GetSection(AadhaarVaultOptions.SectionName).Bind(aadhaarVaultOptions);
    services.AddSingleton(Options.Create(aadhaarVaultOptions));
    services.AddHttpClient<IMockAadhaarApiClient, MockAadhaarApiClient>(client =>
    {
      client.BaseAddress = new Uri(aadhaarVaultOptions.MockApiBaseUrl);
      client.Timeout = TimeSpan.FromSeconds(10);
    });
    services.AddScoped<IAadhaarVaultService, AadhaarVaultService>();
    var encryptionOptions = new EncryptionOptions();
    configuration.GetSection(EncryptionOptions.SectionName).Bind(encryptionOptions);
    services.AddSingleton(Options.Create(encryptionOptions));
    services.AddSingleton<IPiiFieldEncryptionService, PiiFieldEncryptionService>();
    services.AddSingleton<IBlindIndexService, BlindIndexService>();

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
      .AddCheck("self", () => HealthCheckResult.Healthy("Application is running."), tags: ["live"])
      .AddCheck<PostgreSqlConnectionHealthCheck>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "ready"],
        timeout: TimeSpan.FromSeconds(healthCheckTimeoutSeconds));

    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
      healthChecks.AddCheck<RedisDistributedCacheHealthCheck>("redis", tags: ["cache", "ready"]);
    }

    healthChecks
      .AddCheck<S3BucketHealthCheck>(
        "s3",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["aws", "ready"],
        timeout: TimeSpan.FromSeconds(healthCheckTimeoutSeconds))
      .AddCheck<CognitoUserPoolHealthCheck>(
        "cognito",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["aws", "ready"],
        timeout: TimeSpan.FromSeconds(healthCheckTimeoutSeconds));

    // Register AWS services (S3, SES)
    services.AddAwsServices(configuration);

    return services;
  }
}
