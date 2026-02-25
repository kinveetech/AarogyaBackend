namespace Aarogya.Infrastructure.Aadhaar;

public sealed record MockAadhaarValidationRequest(
  string AadhaarNumber,
  string? FirstName = null,
  string? LastName = null,
  DateOnly? DateOfBirth = null);

public sealed record MockAadhaarValidationResponse(
  bool IsValid,
  string? RequestId,
  string? Message,
  string? Provider,
  MockAadhaarDemographics? Demographics);

public sealed record MockAadhaarDemographics(
  string? FullName,
  DateOnly? DateOfBirth,
  string? Gender,
  string? Address);

public sealed record MockAadhaarTokenizeRequest(string AadhaarNumberHashBase64);

public sealed record MockAadhaarTokenizeResponse(Guid ReferenceToken, string? RequestId);
