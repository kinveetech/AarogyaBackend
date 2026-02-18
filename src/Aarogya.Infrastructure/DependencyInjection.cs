using Aarogya.Infrastructure.Aws;
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

    // Register a health check for PostgreSQL
    services.AddHealthChecks()
      .AddDbContextCheck<AarogyaDbContext>("postgresql", tags: ["db", "ready"]);

    // Register AWS services (S3, SES)
    services.AddAwsServices(configuration);

    return services;
  }
}
