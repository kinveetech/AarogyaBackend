using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record UserProfileResponse(
  string Sub,
  string Email,
  string FirstName,
  string LastName,
  string? Phone,
  string? Address,
  string? BloodGroup,
  DateOnly? DateOfBirth,
  string? Gender,
  string RegistrationStatus,
  IReadOnlyList<string> Roles);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record UpdateUserProfileRequest(
  string? FirstName,
  string? LastName,
  string? Email,
  string? Phone,
  string? Address,
  string? BloodGroup,
  DateOnly? DateOfBirth);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record VerifyAadhaarRequest(string AadhaarNumber);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record AadhaarVerificationResponse(
  Guid ReferenceToken,
  bool ExistingRecord,
  string? Provider,
  AadhaarDemographicsResponse? Demographics);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record AadhaarDemographicsResponse(
  string? FullName,
  DateOnly? DateOfBirth,
  string? Gender,
  string? Address);
