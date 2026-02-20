using Aarogya.Api.Authentication;
using Aarogya.Domain.Entities;
using Aarogya.Domain.ValueObjects;
using Aarogya.Infrastructure.Persistence;
using Aarogya.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Security;

internal sealed class DataEncryptionKeyRotationHostedService(
  IServiceScopeFactory scopeFactory,
  IOptions<DataEncryptionRotationOptions> rotationOptions,
  ILogger<DataEncryptionKeyRotationHostedService> logger)
  : BackgroundService
{
  private readonly DataEncryptionRotationOptions _rotationOptions = rotationOptions.Value;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!_rotationOptions.EnableBackgroundReEncryption)
    {
      logger.LogInformation("Background data re-encryption is disabled.");
      return;
    }

    await RunRotationCycleAsync(stoppingToken);

    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_rotationOptions.CheckIntervalMinutes));
    while (!stoppingToken.IsCancellationRequested
      && await timer.WaitForNextTickAsync(stoppingToken))
    {
      await RunRotationCycleAsync(stoppingToken);
    }
  }

  private async Task RunRotationCycleAsync(CancellationToken cancellationToken)
  {
    await using var scope = scopeFactory.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AarogyaDbContext>();
    var utcClock = scope.ServiceProvider.GetRequiredService<IUtcClock>();
    var encryptionService = scope.ServiceProvider.GetRequiredService<IPiiFieldEncryptionService>();
    var rotationService = scope.ServiceProvider.GetRequiredService<IDataEncryptionKeyRotationService>();

    var activeKeyId = encryptionService.ActiveKeyId;
    if (await HasCompletedRotationForKeyAsync(dbContext, activeKeyId, cancellationToken))
    {
      logger.LogDebug("Data re-encryption already completed for key {ActiveKeyId}; skipping cycle.", activeKeyId);
      return;
    }

    logger.LogInformation("Starting data re-encryption cycle for key {ActiveKeyId}", activeKeyId);
    await WriteSystemRotationAuditAsync(
      dbContext,
      utcClock.UtcNow,
      "encryption.reencryption.started",
      $"Data re-encryption started for key '{activeKeyId}'.",
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["keyId"] = activeKeyId
      },
      200,
      cancellationToken);

    try
    {
      var summary = await rotationService.ReEncryptAllAsync(cancellationToken);
      await WriteSystemRotationAuditAsync(
        dbContext,
        utcClock.UtcNow,
        "encryption.reencryption.completed",
        $"Data re-encryption completed for key '{summary.ActiveKeyId}'.",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["keyId"] = summary.ActiveKeyId,
          ["usersTouched"] = summary.UsersTouched.ToString(System.Globalization.CultureInfo.InvariantCulture),
          ["emergencyContactsTouched"] = summary.EmergencyContactsTouched.ToString(System.Globalization.CultureInfo.InvariantCulture),
          ["aadhaarRecordsTouched"] = summary.AadhaarRecordsTouched.ToString(System.Globalization.CultureInfo.InvariantCulture),
          ["totalTouched"] = summary.TotalTouched.ToString(System.Globalization.CultureInfo.InvariantCulture)
        },
        200,
        cancellationToken);

      logger.LogInformation(
        "Data re-encryption completed for key {ActiveKeyId}. Users={Users}, EmergencyContacts={Contacts}, AadhaarRecords={Aadhaar}.",
        summary.ActiveKeyId,
        summary.UsersTouched,
        summary.EmergencyContactsTouched,
        summary.AadhaarRecordsTouched);
    }
    catch (Exception ex)
    {
      await WriteSystemRotationAuditAsync(
        dbContext,
        utcClock.UtcNow,
        "encryption.reencryption.failed",
        $"Data re-encryption failed for key '{activeKeyId}'.",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["keyId"] = activeKeyId,
          ["errorType"] = ex.GetType().Name
        },
        500,
        cancellationToken);

      logger.LogError(ex, "Data re-encryption failed for key {ActiveKeyId}", activeKeyId);
    }
  }

  private static async Task<bool> HasCompletedRotationForKeyAsync(
    AarogyaDbContext dbContext,
    string activeKeyId,
    CancellationToken cancellationToken)
  {
    var recentCompletions = await dbContext.AuditLogs
      .AsNoTracking()
      .Where(x => x.Action == "encryption.reencryption.completed")
      .OrderByDescending(x => x.OccurredAt)
      .Take(25)
      .ToListAsync(cancellationToken);

    return recentCompletions.Exists(
      x => x.Details.Data.TryGetValue("keyId", out var keyId)
        && string.Equals(keyId, activeKeyId, StringComparison.Ordinal));
  }

  private static async Task WriteSystemRotationAuditAsync(
    AarogyaDbContext dbContext,
    DateTimeOffset occurredAt,
    string action,
    string summary,
    Dictionary<string, string> metadata,
    int resultStatus,
    CancellationToken cancellationToken)
  {
    dbContext.AuditLogs.Add(new AuditLog
    {
      Id = Guid.NewGuid(),
      OccurredAt = occurredAt,
      Action = action,
      EntityType = "encryption_key",
      ResultStatus = resultStatus,
      Details = new AuditLogDetails
      {
        Summary = summary,
        Data = metadata
      }
    });

    await dbContext.SaveChangesAsync(cancellationToken);
  }
}
