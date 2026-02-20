using System.Globalization;
using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Api.Features.V1.Users;

internal sealed class UserDataRightsService(
  IUserRepository userRepository,
  IReportRepository reportRepository,
  IAccessGrantRepository accessGrantRepository,
  IEmergencyContactRepository emergencyContactRepository,
  IConsentRecordRepository consentRecordRepository,
  IAuditLogRepository auditLogRepository,
  IUnitOfWork unitOfWork,
  IAuditLoggingService auditLoggingService,
  IUtcClock clock)
  : IUserDataRightsService
{
  private static readonly IReadOnlyList<string> DeletionRetentionExceptions =
  [
    "Anonymized audit logs are retained for security and legal compliance."
  ];

  public async Task<DataExportResponse> ExportCurrentUserDataAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var user = await ResolveUserAsync(userSub, cancellationToken);

    var reports = await reportRepository.ListAsync(new ReportsByRelatedUserSpecification(user.Id, includeDeleted: true), cancellationToken);
    var grants = await accessGrantRepository.ListAsync(new AccessGrantsByUserSpecification(user.Id), cancellationToken);
    var emergencyContacts = await emergencyContactRepository.ListByUserAsync(user.Id, cancellationToken);
    var consents = await consentRecordRepository.ListAsync(new ConsentRecordsByUserSpecification(user.Id), cancellationToken);
    var auditLogs = await auditLogRepository.ListByActorAsync(user.Id, cancellationToken);

    var response = new DataExportResponse(
      clock.UtcNow,
      new UserProfileExportData(
        user.Id,
        user.ExternalAuthId,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Phone,
        user.Address,
        user.BloodGroup,
        user.DateOfBirth,
        user.IsActive,
        user.Role.ToString()),
      reports.Select(report => new ReportExportData(
        report.Id,
        report.ReportNumber,
        report.ReportType.ToString(),
        report.Status.ToString(),
        report.UploadedAt,
        report.SourceSystem,
        report.IsDeleted)).ToArray(),
      grants.Select(grant => new AccessGrantExportData(
        grant.Id,
        grant.PatientId,
        grant.GrantedToUserId,
        grant.GrantedByUserId,
        grant.Status.ToString(),
        grant.GrantReason,
        grant.StartsAt,
        grant.ExpiresAt,
        grant.RevokedAt)).ToArray(),
      emergencyContacts.Select(contact => new EmergencyContactExportData(
        contact.Id,
        contact.Name,
        contact.Relationship,
        contact.Phone,
        contact.Email,
        contact.IsPrimary,
        contact.CreatedAt)).ToArray(),
      consents.Select(record => new ConsentRecordExportData(
        record.Id,
        record.Purpose,
        record.IsGranted,
        record.Source,
        record.OccurredAt)).ToArray(),
      auditLogs.Select(log => new AuditLogExportData(
        log.Id,
        log.OccurredAt,
        log.Action,
        log.EntityType,
        log.EntityId,
        log.ResultStatus)).ToArray());

    await auditLoggingService.LogDataAccessAsync(
      user,
      "user_data.export_requested",
      "user",
      user.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["reportsCount"] = response.Reports.Count.ToString(CultureInfo.InvariantCulture),
        ["accessGrantsCount"] = response.AccessGrants.Count.ToString(CultureInfo.InvariantCulture)
      },
      cancellationToken);

    return response;
  }

  public async Task<DataDeletionResponse> DeleteCurrentUserDataAsync(
    string userSub,
    DataDeletionRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    if (!request.ConfirmPermanentDeletion)
    {
      throw new InvalidOperationException("ConfirmPermanentDeletion must be true to proceed.");
    }

    var user = await ResolveUserAsync(userSub, cancellationToken);
    var now = clock.UtcNow;

    var reports = await reportRepository.ListAsync(new ReportsByRelatedUserSpecification(user.Id, includeDeleted: true), cancellationToken);
    var grants = await accessGrantRepository.ListAsync(new AccessGrantsByUserSpecification(user.Id), cancellationToken);
    var contacts = await emergencyContactRepository.ListByUserAsync(user.Id, cancellationToken);
    var consents = await consentRecordRepository.ListAsync(new ConsentRecordsByUserSpecification(user.Id), cancellationToken);
    var auditLogs = await auditLogRepository.ListByActorAsync(user.Id, cancellationToken);

    foreach (var report in reports)
    {
      report.Results.Notes = null;
      report.Results.Parameters = [];
      report.Metadata.Tags.Clear();
      report.Parameters.Clear();
      report.FileStorageKey = null;
      report.ChecksumSha256 = null;
      report.IsDeleted = true;
      report.DeletedAt = now;
      report.HardDeletedAt = now;
      report.UpdatedAt = now;
      reportRepository.Update(report);
    }

    foreach (var grant in grants)
    {
      accessGrantRepository.Delete(grant);
    }

    foreach (var contact in contacts)
    {
      emergencyContactRepository.Delete(contact);
    }

    foreach (var consent in consents)
    {
      consentRecordRepository.Delete(consent);
    }

    foreach (var auditLog in auditLogs)
    {
      auditLog.ActorUserId = null;
      auditLog.UserAgent = null;
      auditLog.ClientIp = null;
      auditLog.Details.Summary = "Anonymized after user deletion request.";
      auditLog.Details.Data = RedactAuditData(auditLog.Details.Data);
      auditLogRepository.Update(auditLog);
    }

    AnonymizeUser(user, now);
    userRepository.Update(user);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    await auditLoggingService.LogDataAccessAsync(
      user,
      "user_data.deletion_requested",
      "user",
      user.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["reason"] = InputSanitizer.SanitizeNullablePlainText(request.Reason) ?? string.Empty,
        ["reportsAffected"] = reports.Count.ToString(CultureInfo.InvariantCulture),
        ["accessGrantsAffected"] = grants.Count.ToString(CultureInfo.InvariantCulture)
      },
      cancellationToken);

    return new DataDeletionResponse(
      now,
      new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
      {
        ["reports"] = reports.Count,
        ["accessGrants"] = grants.Count,
        ["emergencyContacts"] = contacts.Count,
        ["consentRecords"] = consents.Count,
        ["auditLogsAnonymized"] = auditLogs.Count,
        ["users"] = 1
      },
      DeletionRetentionExceptions);
  }

  private async Task<User> ResolveUserAsync(string userSub, CancellationToken cancellationToken)
  {
    return await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new KeyNotFoundException("Authenticated user is not provisioned in the database.");
  }

  private static void AnonymizeUser(User user, DateTimeOffset now)
  {
    user.ExternalAuthId = null;
    user.FirstName = "Deleted";
    user.LastName = "User";
    user.Email = $"deleted-{user.Id:D}@deleted.local";
    user.Phone = null;
    user.Address = null;
    user.BloodGroup = null;
    user.DateOfBirth = null;
    user.EmailHash = null;
    user.PhoneHash = null;
    user.AadhaarRefToken = null;
    user.AadhaarSha256 = null;
    user.IsActive = false;
    user.UpdatedAt = now;
  }

  private static Dictionary<string, string> RedactAuditData(IReadOnlyDictionary<string, string>? source)
  {
    if (source is null || source.Count == 0)
    {
      return [];
    }

    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (key, value) in source)
    {
      if (key.Contains("email", StringComparison.OrdinalIgnoreCase)
        || key.Contains("phone", StringComparison.OrdinalIgnoreCase)
        || key.Contains("aadhaar", StringComparison.OrdinalIgnoreCase)
        || key.Contains("address", StringComparison.OrdinalIgnoreCase)
        || key.Contains("name", StringComparison.OrdinalIgnoreCase))
      {
        result[key] = "[REDACTED]";
      }
      else
      {
        result[key] = value;
      }
    }

    return result;
  }
}
