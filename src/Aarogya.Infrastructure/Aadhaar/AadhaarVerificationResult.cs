namespace Aarogya.Infrastructure.Aadhaar;

public sealed record AadhaarVerificationResult(
  Guid ReferenceToken,
  bool IsExistingRecord,
  string? Provider,
  MockAadhaarDemographics? Demographics,
  string? RequestId);
