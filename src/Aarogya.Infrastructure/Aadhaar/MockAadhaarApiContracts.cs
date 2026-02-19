namespace Aarogya.Infrastructure.Aadhaar;

public sealed record MockAadhaarValidationRequest(string AadhaarNumber);

public sealed record MockAadhaarValidationResponse(bool IsValid, string? RequestId, string? Message);

public sealed record MockAadhaarTokenizeRequest(string AadhaarNumberHashBase64);

public sealed record MockAadhaarTokenizeResponse(Guid ReferenceToken, string? RequestId);
