using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record RegisterUserRequest(
  string Role,
  string FirstName,
  string LastName,
  string Email,
  string? Phone,
  DateOnly? DateOfBirth,
  string? Gender,
  string? Address,
  string? BloodGroup,
  DoctorRegistrationData? DoctorData,
  LabTechnicianRegistrationData? LabTechnicianData,
  IReadOnlyList<InitialConsentGrant>? Consents);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record DoctorRegistrationData(
  string MedicalLicenseNumber,
  string Specialization,
  string? ClinicOrHospitalName,
  string? ClinicAddress);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record LabTechnicianRegistrationData(
  string LabName,
  string? LabLicenseNumber,
  string? NablAccreditationId);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record InitialConsentGrant(string Purpose, bool IsGranted);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record RegisterUserResponse(
  string Sub,
  string Role,
  string RegistrationStatus,
  string Email,
  string FirstName,
  string LastName,
  string? Phone,
  string? Address,
  string? BloodGroup,
  DateOnly? DateOfBirth,
  string? Gender,
  IReadOnlyList<string> ConsentsGranted);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record RegistrationStatusResponse(
  string Sub,
  string Role,
  string RegistrationStatus,
  string? RejectionReason);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record ApproveRegistrationRequest(string? Notes);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record RejectRegistrationRequest(string Reason);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record PendingRegistrationResponse(
  string Sub,
  string Role,
  string FirstName,
  string LastName,
  string Email,
  DateTimeOffset RegisteredAt,
  DoctorRegistrationData? DoctorData,
  LabTechnicianRegistrationData? LabTechnicianData);
