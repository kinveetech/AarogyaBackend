using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Aarogya.Api.Features.V1.EmergencyAccess;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record CreateEmergencyAccessRequest(
  [property: JsonRequired] string PatientSub,
  [property: JsonRequired] string EmergencyContactPhone,
  [property: JsonRequired] string DoctorSub,
  [property: JsonRequired] string Reason,
  int? DurationHours = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Returned by public API action signature.")]
public sealed record EmergencyAccessResponse(
  Guid GrantId,
  string PatientSub,
  string DoctorSub,
  Guid EmergencyContactId,
  DateTimeOffset StartsAt,
  DateTimeOffset ExpiresAt,
  string Purpose);
