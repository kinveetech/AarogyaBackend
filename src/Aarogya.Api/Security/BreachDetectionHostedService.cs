using System.Globalization;
using Aarogya.Api.Authentication;
using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Security;

internal sealed class BreachDetectionHostedService(
  IServiceScopeFactory scopeFactory,
  IOptions<BreachDetectionOptions> options,
  IUtcClock clock,
  ILogger<BreachDetectionHostedService> logger)
  : BackgroundService
{
  private const string SecurityAlertEventType = "security_alert";

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var config = options.Value;
    if (!config.EnableWorker)
    {
      logger.LogInformation("Breach detection worker is disabled.");
      return;
    }

    var interval = TimeSpan.FromMinutes(Math.Max(1, config.ScanIntervalMinutes));
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
        logger.LogError(ex, "Breach detection cycle failed.");
      }

      await Task.Delay(interval, stoppingToken);
    }
  }

  internal async Task RunCycleAsync(CancellationToken cancellationToken = default)
  {
    await using var scope = scopeFactory.CreateAsyncScope();
    var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var transactionalEmailSender = scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
    var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
    var cacheService = scope.ServiceProvider.GetRequiredService<IEntityCacheService>();

    var config = options.Value;
    var now = clock.UtcNow;
    var from = now.AddMinutes(-Math.Max(1, config.LookbackWindowMinutes));
    var suspiciousActions = new HashSet<string>(config.SuspiciousActions, StringComparer.OrdinalIgnoreCase);
    var bulkActions = new HashSet<string>(config.BulkExportActions, StringComparer.OrdinalIgnoreCase);

    var logs = await auditLogRepository.ListAsync(
      new RecentAuditLogsSpecification(from, now),
      cancellationToken);

    var byActor = logs
      .Where(log => log.ActorUserId.HasValue)
      .GroupBy(log => log.ActorUserId!.Value);

    foreach (var group in byActor)
    {
      var actorId = group.Key;
      var suspiciousCount = group.Count(log => suspiciousActions.Contains(log.Action));
      var bulkCount = group.Count(log => bulkActions.Contains(log.Action));

      if (suspiciousCount < config.SuspiciousAccessThresholdPerActor
        && bulkCount < config.BulkExportThresholdPerActor)
      {
        continue;
      }

      var actor = await userRepository.GetByIdAsync(actorId, cancellationToken);
      if (actor is null)
      {
        continue;
      }

      var dedupeKey = BuildDedupeKey(actorId, from, now, suspiciousCount, bulkCount);
      var alreadySent = await cacheService.GetAsync<string>(dedupeKey, cancellationToken);
      if (!string.IsNullOrWhiteSpace(alreadySent))
      {
        continue;
      }

      var message = BuildMessage(actor, from, now, suspiciousCount, bulkCount);
      await NotifyAsync(config, actor, message, suspiciousCount, bulkCount, from, now,
        transactionalEmailSender, pushNotificationService, cancellationToken);
      await cacheService.SetAsync(
        dedupeKey,
        "1",
        TimeSpan.FromMinutes(Math.Max(2, config.LookbackWindowMinutes * 2)),
        cancellationToken);
    }
  }

  private async Task NotifyAsync(
    BreachDetectionOptions config,
    User actor,
    string message,
    int suspiciousCount,
    int bulkCount,
    DateTimeOffset from,
    DateTimeOffset to,
    ITransactionalEmailSender transactionalEmailSender,
    IPushNotificationService pushNotificationService,
    CancellationToken cancellationToken)
  {
    var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["actorUserId"] = actor.Id.ToString("D"),
      ["suspiciousCount"] = suspiciousCount.ToString(CultureInfo.InvariantCulture),
      ["bulkCount"] = bulkCount.ToString(CultureInfo.InvariantCulture),
      ["windowFromUtc"] = from.ToString("O", CultureInfo.InvariantCulture),
      ["windowToUtc"] = to.ToString("O", CultureInfo.InvariantCulture)
    };

    if (config.NotifyImpactedUsers && !string.IsNullOrWhiteSpace(actor.Email))
    {
      await transactionalEmailSender.SendAsync(
        actor.Email,
        $"{actor.FirstName} {actor.LastName}".Trim(),
        "Security Alert: Unusual Access Activity",
        $"<p>{message}</p>",
        message,
        cancellationToken);

      if (!string.IsNullOrWhiteSpace(actor.ExternalAuthId))
      {
        await pushNotificationService.SendToUserAsync(
          actor.ExternalAuthId,
          SecurityAlertEventType,
          new SendPushNotificationRequest(
            "Security Alert",
            message,
            metadata),
          cancellationToken);
      }
    }

    if (!config.NotifyAuthorities || config.AuthorityEmails.Count == 0)
    {
      return;
    }

    var authoritySubject = $"Security Incident Alert - Actor {actor.Id:D}";
    foreach (var email in config.AuthorityEmails.Where(address => !string.IsNullOrWhiteSpace(address)))
    {
      await transactionalEmailSender.SendAsync(
        email.Trim(),
        null,
        authoritySubject,
        $"<p>{message}</p>",
        message,
        cancellationToken);
    }

    logger.LogWarning(
      "Security alert emitted for actor {ActorUserId}. suspiciousCount={SuspiciousCount} bulkCount={BulkCount}",
      actor.Id,
      suspiciousCount,
      bulkCount);
  }

  private static string BuildMessage(
    User actor,
    DateTimeOffset from,
    DateTimeOffset to,
    int suspiciousCount,
    int bulkCount)
  {
    return string.Create(
      CultureInfo.InvariantCulture,
      $"Potential breach activity detected for user {actor.Id:D} between {from:O} and {to:O}. "
      + $"Suspicious access events: {suspiciousCount}. Bulk export events: {bulkCount}.");
  }

  private static string BuildDedupeKey(
    Guid actorUserId,
    DateTimeOffset from,
    DateTimeOffset to,
    int suspiciousCount,
    int bulkCount)
  {
    return string.Create(
      CultureInfo.InvariantCulture,
      $"security:breach-alert:{actorUserId:D}:{from.ToUnixTimeSeconds()}:{to.ToUnixTimeSeconds()}:{suspiciousCount}:{bulkCount}");
  }
}
