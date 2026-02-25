namespace Aarogya.Infrastructure.Aadhaar;

public interface IMockAadhaarApiClient
{
  public Task<MockAadhaarValidationResponse> ValidateAsync(
    string aadhaarNumber,
    string? firstName = null,
    string? lastName = null,
    DateOnly? dateOfBirth = null,
    CancellationToken cancellationToken = default);

  public Task<MockAadhaarTokenizeResponse> TokenizeAsync(byte[] aadhaarSha256, CancellationToken cancellationToken = default);
}
