using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Aarogya.Api.Auditing;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Infrastructure.Persistence;
using Aarogya.Infrastructure.Security;
using Aarogya.Infrastructure.Seeding;
using Amazon.CloudFront;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Aarogya.Api.Tests.Fixtures;

public sealed class ApiPostgreSqlWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
  private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .WithDatabase("postgres")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();

  private string _connectionString = string.Empty;

  public async Task InitializeAsync()
  {
    await _container.StartAsync();
    _connectionString = await CreateIsolatedDatabaseConnectionStringAsync(CancellationToken.None);
    _ = CreateClient();
    await SeedUsersAsync();
  }

  [SuppressMessage(
    "Style",
    "IDE0002:Name can be simplified",
    Justification = "Explicit base dispose call is intentional for WebApplicationFactory cleanup.")]
  public new async Task DisposeAsync()
  {
    base.Dispose();
    await _container.DisposeAsync();
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
    builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
    builder.UseDefaultServiceProvider((_, options) =>
    {
      options.ValidateOnBuild = false;
      options.ValidateScopes = false;
    });

    builder.ConfigureAppConfiguration((_, configBuilder) =>
    {
      configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:DefaultConnection"] = _connectionString,
        ["Database:AutoMigrateOnStartup"] = "true",
        ["SeedData:EnableOnStartup"] = "false",
        ["Aws:UseLocalStack"] = "true",
        ["Aws:ServiceUrl"] = "http://localhost:4566",
        ["Aws:S3:BucketName"] = "aarogya-dev",
        ["Aws:Sqs:QueueName"] = "aarogya-dev-queue",
        ["Aws:Sqs:ConfigureS3NotificationsOnStartup"] = "false",
        ["Aws:Sqs:EnableUploadEventConsumer"] = "false",
        ["VirusScanning:EnableScanning"] = "false",
        ["FileDeletion:EnableHardDeleteWorker"] = "false",
        ["EncryptionRotation:EnableBackgroundReEncryption"] = "false",
        ["Encryption:UseAwsKms"] = "false",
        ["Encryption:LocalDataKey"] = "integration-tests-local-key",
        ["Encryption:BlindIndexKey"] = "integration-tests-blind-index-key"
      });
    });

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<IAmazonS3>();
      services.RemoveAll<IAmazonSQS>();
      services.RemoveAll<IAmazonCloudFront>();
      services.RemoveAll<IDataSeeder>();
      services.RemoveAll<IAuditLoggingService>();
      services.RemoveAll<IReportFileUploadService>();
      services.RemoveAll<IHostedService>();

      var s3 = new Mock<IAmazonS3>();
      s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PutObjectResponse());
      s3.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
        .ReturnsAsync("https://example.com/signed-url");

      services.AddSingleton(s3.Object);
      services.AddSingleton(Mock.Of<IAmazonSQS>());
      services.AddSingleton(Mock.Of<IAmazonCloudFront>());
      services.AddSingleton<IDataSeeder, NoOpDataSeeder>();
      services.AddSingleton<IAuditLoggingService, NoOpAuditLoggingService>();
      services.AddSingleton<IReportFileUploadService, NoOpReportFileUploadService>();

      services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
      }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
    });
  }

  private async Task SeedUsersAsync()
  {
    await using var scope = Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
    var encryptionService = scope.ServiceProvider.GetRequiredService<IPiiFieldEncryptionService>();

    if (await dbContext.Users.AnyAsync(x => x.ExternalAuthId == "seed-PATIENT-IT"))
    {
      return;
    }

    var now = DateTimeOffset.UtcNow;
    await InsertSeedUserAsync(
      dbContext,
      encryptionService,
      "seed-PATIENT-IT",
      "patient",
      "Seed",
      "Patient",
      "seed.patient.integration@aarogya.dev",
      "+919876543210",
      now);
    await InsertSeedUserAsync(
      dbContext,
      encryptionService,
      "seed-DOCTOR-IT",
      "doctor",
      "Seed",
      "Doctor",
      "seed.doctor.integration@aarogya.dev",
      "+919876543211",
      now);
    await InsertSeedUserAsync(
      dbContext,
      encryptionService,
      "seed-LAB-IT",
      "lab_technician",
      "Seed",
      "Lab",
      "seed.lab.integration@aarogya.dev",
      "+919876543212",
      now);
  }

  public async Task GrantConsentAsync(string userSub, string purpose, string source = "integration-tests")
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(userSub);
    ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

    await using var scope = Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
    var now = DateTimeOffset.UtcNow;

    _ = await dbContext.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO consent_records
      (
        id,
        user_id,
        purpose,
        is_granted,
        source,
        occurred_at,
        created_at,
        updated_at
      )
      SELECT
        {0},
        u.id,
        {1},
        TRUE,
        {2},
        {3},
        {3},
        {3}
      FROM users u
      WHERE u.external_auth_id = {4};
      """,
      Guid.NewGuid(),
      purpose,
      source,
      now,
      userSub);
  }

  private static async Task InsertSeedUserAsync(
    AarogyaDbContext dbContext,
    IPiiFieldEncryptionService encryptionService,
    string externalAuthId,
    string role,
    string firstName,
    string lastName,
    string email,
    string phone,
    DateTimeOffset now)
  {
    _ = await dbContext.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO users
      (
        id,
        external_auth_id,
        role,
        first_name_encrypted,
        last_name_encrypted,
        email_encrypted,
        phone_encrypted,
        is_active,
        created_at,
        updated_at
      )
      VALUES
      (
        {0},
        {1},
        CAST({2} AS user_role),
        {3},
        {4},
        {5},
        {6},
        TRUE,
        {7},
        {7}
      )
      ON CONFLICT (external_auth_id) DO NOTHING;
      """,
      Guid.NewGuid(),
      externalAuthId,
      role,
      encryptionService.Encrypt(firstName)!,
      encryptionService.Encrypt(lastName)!,
      encryptionService.Encrypt(email)!,
      encryptionService.Encrypt(phone)!,
      now);
  }

  [SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Database name is generated internally from a Guid and safely quoted.")]
  private async Task<string> CreateIsolatedDatabaseConnectionStringAsync(CancellationToken cancellationToken)
  {
    var databaseName = $"aarogya_api_test_{Guid.NewGuid():N}";

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

internal sealed class NoOpDataSeeder : IDataSeeder
{
  public Task SeedAsync(CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }
}

internal sealed class NoOpAuditLoggingService : IAuditLoggingService
{
  public Task LogDataAccessAsync(
    User actor,
    string action,
    string resourceType,
    Guid? resourceId,
    int resultStatus,
    IReadOnlyDictionary<string, string>? data = null,
    CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }
}

internal sealed class NoOpReportFileUploadService : IReportFileUploadService
{
  public Task<ReportUploadResponse> UploadAsync(
    string userSub,
    IFormFile file,
    CancellationToken cancellationToken = default)
  {
    var uploadedAt = DateTimeOffset.UtcNow;
    var response = new ReportUploadResponse(
      Guid.NewGuid(),
      $"RPT-{Guid.NewGuid():N}".ToUpperInvariant()[..14],
      $"reports/{userSub}/{Guid.NewGuid():N}.pdf",
      file.ContentType,
      file.Length,
      Convert.ToHexString(System.Security.Cryptography.SHA256.HashData([1, 2, 3])),
      uploadedAt);

    return Task.FromResult(response);
  }
}

internal sealed class TestAuthHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder)
  : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  public const string SchemeName = "IntegrationTestAuth";

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue("Authorization", out var authHeader)
      || string.IsNullOrWhiteSpace(authHeader))
    {
      return Task.FromResult(AuthenticateResult.NoResult());
    }

    var value = authHeader.ToString();
    if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
      return Task.FromResult(AuthenticateResult.Fail("Invalid authorization scheme."));
    }

    var token = value[7..].Trim();
    if (token.Equals("invalid-token", StringComparison.Ordinal))
    {
      return Task.FromResult(AuthenticateResult.Fail("Invalid token."));
    }

    if (token.Equals("expired-token", StringComparison.Ordinal))
    {
      return Task.FromResult(AuthenticateResult.Fail("Token expired."));
    }

    var principal = token switch
    {
      "valid-patient" => CreatePrincipal("seed-PATIENT-IT", "Patient"),
      "valid-doctor" => CreatePrincipal("seed-DOCTOR-IT", "Doctor"),
      "valid-lab" => CreatePrincipal("seed-LAB-IT", "LabTechnician"),
      _ => null
    };

    if (principal is null)
    {
      return Task.FromResult(AuthenticateResult.Fail("Unknown test token."));
    }

    var ticket = new AuthenticationTicket(principal, SchemeName);
    return Task.FromResult(AuthenticateResult.Success(ticket));
  }

  private static ClaimsPrincipal CreatePrincipal(string sub, string role)
  {
    var claims = new List<Claim>
    {
      new("sub", sub),
      new(ClaimTypes.Role, role)
    };

    return new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
  }
}
