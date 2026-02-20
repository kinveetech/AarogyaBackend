using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.EmergencyContacts;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record CreateEmergencyContactRequest(string Name, string PhoneNumber, string Relationship, string? Email = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record UpdateEmergencyContactRequest(string Name, string PhoneNumber, string Relationship, string? Email = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record EmergencyContactResponse(Guid ContactId, string Name, string PhoneNumber, string Relationship, string? Email = null);
