using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.EmergencyAccess;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record EmergencyAccessAuditQueryRequest(
  string? PatientSub = null,
  string? DoctorSub = null,
  Guid? GrantId = null,
  DateTimeOffset? FromUtc = null,
  DateTimeOffset? ToUtc = null,
  int Page = 1,
  int PageSize = 50);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record EmergencyAccessAuditEventResponse(
  Guid AuditLogId,
  DateTimeOffset OccurredAt,
  string Action,
  Guid? GrantId,
  Guid? ActorUserId,
  string? ActorRole,
  string ResourceType,
  Guid? ResourceId,
  IReadOnlyDictionary<string, string> Data);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by API action signatures.")]
public sealed record EmergencyAccessAuditTrailResponse(
  int Page,
  int PageSize,
  int TotalCount,
  IReadOnlyList<EmergencyAccessAuditEventResponse> Items);
