using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Aarogya.Api.Features.V1.AccessGrants;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record CreateAccessGrantRequest(
  string DoctorSub,
  [property: JsonRequired] bool AllReports,
  IReadOnlyList<Guid>? ReportIds,
  [property: JsonRequired] string Purpose,
  DateTimeOffset? ExpiresAt = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record AccessGrantResponse(
  Guid GrantId,
  string DoctorSub,
  bool AllReports,
  IReadOnlyList<Guid> ReportIds,
  string Purpose,
  DateTimeOffset StartsAt,
  DateTimeOffset ExpiresAt,
  bool Revoked);
