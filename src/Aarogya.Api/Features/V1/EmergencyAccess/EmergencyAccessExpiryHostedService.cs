using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.EmergencyAccess;

internal sealed class EmergencyAccessExpiryHostedService(
  IServiceScopeFactory scopeFactory,
  IOptions<EmergencyAccessOptions> options,
  IUtcClock clock,
  ILogger<EmergencyAccessExpiryHostedService> logger)
  : BackgroundService
{
  private const string PreExpiryMarker = "|preexpiry_notified:";

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var config = options.Value;
    if (!config.EnableAutoExpiryWorker)
    {
      logger.LogInformation("Emergency access auto-expiry worker is disabled.");
      return;
    }

    var interval = TimeSpan.FromMinutes(Math.Max(1, config.AutoExpiryWorkerIntervalMinutes));
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await RunCycleAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while processing emergency access auto-expiry cycle.");
      }

      await Task.Delay(interval, stoppingToken);
    }
  }

  internal async Task RunCycleAsync(CancellationToken cancellationToken = default)
  {
    await using var scope = scopeFactory.CreateAsyncScope();
    var accessGrantRepository = scope.ServiceProvider.GetRequiredService<IAccessGrantRepository>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var auditLoggingService = scope.ServiceProvider.GetRequiredService<IAuditLoggingService>();
    var transactionalEmailNotificationService = scope.ServiceProvider.GetRequiredService<ITransactionalEmailNotificationService>();
    var criticalSmsNotificationService = scope.ServiceProvider.GetRequiredService<ICriticalSmsNotificationService>();
    var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
    var cacheService = scope.ServiceProvider.GetService<IEntityCacheService>();

    var now = clock.UtcNow;
    var config = options.Value;
    var dirty = false;

    var dueForPreExpiry = await accessGrantRepository.ListAsync(
      new EmergencyAccessGrantsDueForPreExpiryNotificationSpecification(now, config.PreExpiryNotificationLeadMinutes),
      cancellationToken);

    foreach (var grant in dueForPreExpiry)
    {
      if (grant.Patient is null || grant.GrantedToUser is null)
      {
        continue;
      }

      await SendPreExpiryNotificationsAsync(
        grant.Patient, grant.GrantedToUser, grant,
        transactionalEmailNotificationService, criticalSmsNotificationService, pushNotificationService,
        cancellationToken);
      grant.GrantReason = MarkPreExpiryNotified(grant.GrantReason, now);
      accessGrantRepository.Update(grant);
      dirty = true;

      await auditLoggingService.LogDataAccessAsync(
        grant.Patient,
        "emergency_access.preexpiry_notified",
        "access_grant",
        grant.Id,
        200,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["doctorUserId"] = grant.GrantedToUserId.ToString("D"),
          ["expiresAtUtc"] = grant.ExpiresAt?.ToString("O") ?? string.Empty
        },
        cancellationToken);
    }

    var expired = await accessGrantRepository.ListAsync(
      new EmergencyAccessGrantsExpiredSpecification(now),
      cancellationToken);

    foreach (var grant in expired)
    {
      grant.Status = AccessGrantStatus.Expired;
      grant.RevokedAt = now;
      accessGrantRepository.Update(grant);
      dirty = true;

      if (grant.Patient is null)
      {
        continue;
      }

      await auditLoggingService.LogDataAccessAsync(
        grant.Patient,
        "emergency_access.expired",
        "access_grant",
        grant.Id,
        200,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["expiredAtUtc"] = now.ToString("O")
        },
        cancellationToken);
    }

    if (dirty)
    {
      await unitOfWork.SaveChangesAsync(cancellationToken);
      if (cacheService is not null)
      {
        await cacheService.BumpNamespaceVersionAsync(EntityCacheNamespaces.AccessGrantListings, cancellationToken);
        await cacheService.BumpNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, cancellationToken);
      }
    }
  }

  private async Task SendPreExpiryNotificationsAsync(
    User patient,
    User doctor,
    AccessGrant grant,
    ITransactionalEmailNotificationService transactionalEmailNotificationService,
    ICriticalSmsNotificationService criticalSmsNotificationService,
    IPushNotificationService pushNotificationService,
    CancellationToken cancellationToken)
  {
    var expiresAtUtc = grant.ExpiresAt ?? clock.UtcNow;
    await transactionalEmailNotificationService.SendEmergencyAccessExpiringSoonAsync(patient, doctor, grant, cancellationToken);
    await criticalSmsNotificationService.SendEmergencyAccessExpiringSoonAsync(patient, doctor, grant, cancellationToken);
    await pushNotificationService.SendToUserAsync(
      patient.ExternalAuthId ?? patient.Id.ToString("D"),
      NotificationEventTypes.EmergencyAccess,
      new SendPushNotificationRequest(
        "Emergency Access Expiring Soon",
        $"Emergency access for Dr. {doctor.FirstName} {doctor.LastName} expires at {expiresAtUtc:yyyy-MM-dd HH:mm} UTC.",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["grantId"] = grant.Id.ToString("D"),
          ["doctorUserId"] = doctor.Id.ToString("D"),
          ["expiresAtUtc"] = expiresAtUtc.ToString("O")
        }),
      cancellationToken);
  }

  private static string MarkPreExpiryNotified(string? reason, DateTimeOffset now)
  {
    var value = reason ?? "emergency:unspecified";
    if (value.Contains(PreExpiryMarker, StringComparison.OrdinalIgnoreCase))
    {
      return value;
    }

    return $"{value}{PreExpiryMarker}{now:O}";
  }
}
