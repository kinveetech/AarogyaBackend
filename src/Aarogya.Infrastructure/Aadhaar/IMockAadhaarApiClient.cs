namespace Aarogya.Infrastructure.Aadhaar;

public interface IMockAadhaarApiClient
{
  public Task<MockAadhaarValidationResponse> ValidateAsync(string aadhaarNumber, CancellationToken cancellationToken = default);

  public Task<MockAadhaarTokenizeResponse> TokenizeAsync(byte[] aadhaarSha256, CancellationToken cancellationToken = default);
}
