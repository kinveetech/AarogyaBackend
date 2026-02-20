using Aarogya.Api.Security;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Api.Features.V1.EmergencyAccess;

internal sealed class EmergencyAccessAuditTrailService(
  IAuditLogRepository auditLogRepository,
  IUserRepository userRepository)
  : IEmergencyAccessAuditTrailService
{
  public async Task<EmergencyAccessAuditTrailResponse> QueryAsync(
    EmergencyAccessAuditQueryRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var page = Math.Max(1, request.Page);
    var pageSize = Math.Clamp(request.PageSize, 1, 200);

    var patientId = await ResolveUserIdAsync(request.PatientSub, cancellationToken);
    var doctorId = await ResolveUserIdAsync(request.DoctorSub, cancellationToken);

    var logs = await auditLogRepository.ListAsync(
      new EmergencyAccessAuditLogsSpecification(request.FromUtc, request.ToUtc),
      cancellationToken);

    var filtered = logs.Where(log => Matches(log, request.GrantId, patientId, doctorId)).ToArray();
    var totalCount = filtered.Length;

    var items = filtered
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .Select(Map)
      .ToArray();

    return new EmergencyAccessAuditTrailResponse(page, pageSize, totalCount, items);
  }

  private async Task<Guid?> ResolveUserIdAsync(string? userSub, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(userSub))
    {
      return null;
    }

    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    var user = await userRepository.GetByExternalAuthIdAsync(normalizedSub, cancellationToken);
    return user?.Id;
  }

  private static bool Matches(
    Domain.Entities.AuditLog log,
    Guid? grantId,
    Guid? patientId,
    Guid? doctorId)
  {
    var data = log.Details.Data;

    if (grantId.HasValue)
    {
      var matchesGrantId = log.EntityId == grantId.Value
        || (data.TryGetValue("grantId", out var grantIdString)
          && Guid.TryParse(grantIdString, out var parsedGrantId)
          && parsedGrantId == grantId.Value)
        || (data.TryGetValue("emergencyAccessGrantId", out var emergencyGrantId)
          && Guid.TryParse(emergencyGrantId, out var parsedEmergencyGrantId)
          && parsedEmergencyGrantId == grantId.Value);
      if (!matchesGrantId)
      {
        return false;
      }
    }

    if (patientId.HasValue)
    {
      var matchesPatient = log.ActorUserId == patientId.Value
        || (data.TryGetValue("patientUserId", out var patientUserId)
          && Guid.TryParse(patientUserId, out var parsedPatientId)
          && parsedPatientId == patientId.Value);
      if (!matchesPatient)
      {
        return false;
      }
    }

    if (doctorId.HasValue)
    {
      var matchesDoctor = log.ActorUserId == doctorId.Value
        || (data.TryGetValue("doctorUserId", out var doctorUserId)
          && Guid.TryParse(doctorUserId, out var parsedDoctorId)
          && parsedDoctorId == doctorId.Value);
      if (!matchesDoctor)
      {
        return false;
      }
    }

    return true;
  }

  private static EmergencyAccessAuditEventResponse Map(Domain.Entities.AuditLog log)
  {
    log.Details.Data.TryGetValue("grantId", out var grantIdValue);
    var parsedGrantId = Guid.TryParse(grantIdValue, out var grantId) ? grantId : log.EntityId;

    return new EmergencyAccessAuditEventResponse(
      log.Id,
      log.OccurredAt,
      log.Action,
      parsedGrantId,
      log.ActorUserId,
      log.ActorRole?.ToString(),
      log.EntityType,
      log.EntityId,
      new Dictionary<string, string>(log.Details.Data, StringComparer.OrdinalIgnoreCase));
  }
}
