using System.Diagnostics.CodeAnalysis;
using Aarogya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Aarogya.Infrastructure.Tests.Fixtures;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
  private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .WithDatabase("postgres")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();

  public async Task InitializeAsync()
  {
    await _container.StartAsync();
  }

  public async Task DisposeAsync()
  {
    await _container.DisposeAsync();
  }

  public async Task<ServiceProvider> CreateServiceProviderAsync(CancellationToken cancellationToken = default)
  {
    var connectionString = await CreateIsolatedDatabaseConnectionStringAsync(cancellationToken);

    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:DefaultConnection"] = connectionString,
        ["SeedData:EnableOnStartup"] = "false",
        ["Encryption:UseAwsKms"] = "false",
        ["Encryption:LocalDataKey"] = "integration-tests-local-key",
        ["Encryption:BlindIndexKey"] = "integration-tests-blind-index-key",
        ["Aws:AccessKey"] = "test-access-key",
        ["Aws:SecretKey"] = "test-secret-key"
      })
      .Build();

    var services = new ServiceCollection();
    services.AddInfrastructure(configuration);

    var serviceProvider = services.BuildServiceProvider();

    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
    await dbContext.Database.MigrateAsync(cancellationToken);

    return serviceProvider;
  }

  [SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "The database name is generated internally from a Guid and safely quoted.")]
  private async Task<string> CreateIsolatedDatabaseConnectionStringAsync(CancellationToken cancellationToken)
  {
    var databaseName = $"aarogya_test_{Guid.NewGuid():N}";

    await using var connection = new NpgsqlConnection(_container.GetConnectionString());
    await connection.OpenAsync(cancellationToken);

    await using var command = connection.CreateCommand();
    command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
    await command.ExecuteNonQueryAsync(cancellationToken);

    var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
    {
      Database = databaseName
    };

    return builder.ConnectionString;
  }
}
