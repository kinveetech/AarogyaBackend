using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record DataExportResponse(
  DateTimeOffset GeneratedAtUtc,
  UserProfileExportData Profile,
  IReadOnlyList<ReportExportData> Reports,
  IReadOnlyList<AccessGrantExportData> AccessGrants,
  IReadOnlyList<EmergencyContactExportData> EmergencyContacts,
  IReadOnlyList<ConsentRecordExportData> Consents,
  IReadOnlyList<AuditLogExportData> AuditLogs);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record UserProfileExportData(
  Guid UserId,
  string? Sub,
  string Email,
  string FirstName,
  string LastName,
  string? Phone,
  string? Address,
  string? BloodGroup,
  DateOnly? DateOfBirth,
  bool IsActive,
  string Role);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record ReportExportData(
  Guid ReportId,
  string ReportNumber,
  string ReportType,
  string Status,
  DateTimeOffset UploadedAt,
  string? SourceSystem,
  bool IsDeleted);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record AccessGrantExportData(
  Guid GrantId,
  Guid PatientUserId,
  Guid GrantedToUserId,
  Guid GrantedByUserId,
  string Status,
  string? Purpose,
  DateTimeOffset StartsAt,
  DateTimeOffset? ExpiresAt,
  DateTimeOffset? RevokedAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record EmergencyContactExportData(
  Guid EmergencyContactId,
  string Name,
  string Relationship,
  string Phone,
  string? Email,
  bool IsPrimary,
  DateTimeOffset CreatedAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record ConsentRecordExportData(
  Guid ConsentRecordId,
  string Purpose,
  bool IsGranted,
  string Source,
  DateTimeOffset OccurredAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record AuditLogExportData(
  Guid AuditLogId,
  DateTimeOffset OccurredAt,
  string Action,
  string EntityType,
  Guid? EntityId,
  int? ResultStatus);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record DataDeletionRequest(
  [property: JsonRequired] bool ConfirmPermanentDeletion,
  string? Reason = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record DataDeletionResponse(
  DateTimeOffset DeletedAtUtc,
  IReadOnlyDictionary<string, int> AffectedRecords,
  IReadOnlyList<string> RetentionExceptions);
