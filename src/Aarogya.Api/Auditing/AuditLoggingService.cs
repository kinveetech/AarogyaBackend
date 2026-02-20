using System.Diagnostics;
using System.Globalization;
using Aarogya.Api.Authentication;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.ValueObjects;

namespace Aarogya.Api.Auditing;

internal sealed class AuditLoggingService(
  IAuditLogRepository auditLogRepository,
  IUnitOfWork unitOfWork,
  IHttpContextAccessor httpContextAccessor,
  IUtcClock clock,
  ILogger<AuditLoggingService> logger)
  : IAuditLoggingService
{
  private static readonly Lazy<TimeZoneInfo> IstTimeZone = new(ResolveIstTimeZone);

  public async Task LogDataAccessAsync(
    User actor,
    string action,
    string resourceType,
    Guid? resourceId,
    int resultStatus,
    IReadOnlyDictionary<string, string>? data = null,
    CancellationToken cancellationToken = default)
  {
    var now = clock.UtcNow;
    var request = httpContextAccessor.HttpContext?.Request;
    var occurredAtIst = TimeZoneInfo.ConvertTime(now, IstTimeZone.Value);

    var details = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["occurredAtUtc"] = now.ToString("O", CultureInfo.InvariantCulture),
      ["occurredAtIst"] = occurredAtIst.ToString("O", CultureInfo.InvariantCulture)
    };

    if (data is not null)
    {
      foreach (var pair in data)
      {
        details[pair.Key] = pair.Value;
      }
    }

    var auditLog = new AuditLog
    {
      Id = Guid.NewGuid(),
      OccurredAt = now,
      ActorUserId = actor.Id,
      ActorRole = actor.Role,
      Action = action,
      EntityType = resourceType,
      EntityId = resourceId,
      CorrelationId = TryResolveCorrelationId(request?.HttpContext),
      RequestPath = request?.Path.Value,
      RequestMethod = request?.Method,
      ClientIp = request?.HttpContext.Connection.RemoteIpAddress,
      UserAgent = request?.Headers.UserAgent.ToString(),
      ResultStatus = resultStatus,
      Details = new AuditLogDetails
      {
        Summary = $"{resourceType} access captured.",
        Data = details
      }
    };

    await auditLogRepository.AddAsync(auditLog, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    logger.LogInformation(
      "AuditEvent ActorUserId={ActorUserId} ActorRole={ActorRole} Action={Action} ResourceType={ResourceType} ResourceId={ResourceId} Status={Status} RequestPath={RequestPath}",
      auditLog.ActorUserId,
      auditLog.ActorRole,
      auditLog.Action,
      auditLog.EntityType,
      auditLog.EntityId,
      auditLog.ResultStatus,
      auditLog.RequestPath);
  }

  private static Guid? TryResolveCorrelationId(HttpContext? context)
  {
    if (context is null)
    {
      return null;
    }

    if (Activity.Current is not null && Guid.TryParse(Activity.Current.TraceId.ToString()[..32], out var traceGuid))
    {
      return traceGuid;
    }

    return Guid.TryParse(context.TraceIdentifier, out var correlationId)
      ? correlationId
      : null;
  }

  private static TimeZoneInfo ResolveIstTimeZone()
  {
    try
    {
      return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    }
    catch (TimeZoneNotFoundException)
    {
      return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    }
  }
}
