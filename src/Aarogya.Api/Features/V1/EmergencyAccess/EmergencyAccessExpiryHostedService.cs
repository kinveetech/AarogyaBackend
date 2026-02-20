using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.EmergencyAccess;

internal sealed class EmergencyAccessExpiryHostedService(
  IAccessGrantRepository accessGrantRepository,
  IUnitOfWork unitOfWork,
  IAuditLoggingService auditLoggingService,
  ITransactionalEmailNotificationService transactionalEmailNotificationService,
  ICriticalSmsNotificationService criticalSmsNotificationService,
  IPushNotificationService pushNotificationService,
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

      await SendPreExpiryNotificationsAsync(grant.Patient, grant.GrantedToUser, grant, cancellationToken);
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
    }
  }

  private async Task SendPreExpiryNotificationsAsync(
    User patient,
    User doctor,
    AccessGrant grant,
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
