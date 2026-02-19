using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Aarogya.Infrastructure.Aadhaar;

public sealed class MockAadhaarApiClient(HttpClient httpClient, IOptions<AadhaarVaultOptions> options)
  : IMockAadhaarApiClient
{
  private readonly AadhaarVaultOptions _options = options.Value;

  public async Task<MockAadhaarValidationResponse> ValidateAsync(string aadhaarNumber, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(aadhaarNumber))
    {
      return new MockAadhaarValidationResponse(false, null, "Aadhaar number is required.");
    }

    if (!_options.UseMockApi)
    {
      return new MockAadhaarValidationResponse(true, null, "Mock API disabled.");
    }

    var request = new MockAadhaarValidationRequest(aadhaarNumber);
    var response = await httpClient.PostAsJsonAsync(_options.ValidateEndpoint, request, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      return new MockAadhaarValidationResponse(false, null, $"Mock Aadhaar validate API returned {response.StatusCode}.");
    }

    return await response.Content.ReadFromJsonAsync<MockAadhaarValidationResponse>(cancellationToken)
      ?? new MockAadhaarValidationResponse(false, null, "Invalid mock validation response.");
  }

  public async Task<MockAadhaarTokenizeResponse> TokenizeAsync(byte[] aadhaarSha256, CancellationToken cancellationToken = default)
  {
    if (aadhaarSha256.Length == 0)
    {
      throw new ArgumentException("Aadhaar SHA-256 hash is required.", nameof(aadhaarSha256));
    }

    if (!_options.UseMockApi)
    {
      return new MockAadhaarTokenizeResponse(Guid.NewGuid(), null);
    }

    var request = new MockAadhaarTokenizeRequest(Convert.ToBase64String(aadhaarSha256));
    var response = await httpClient.PostAsJsonAsync(_options.TokenizeEndpoint, request, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      throw new InvalidOperationException($"Mock Aadhaar tokenize API returned {response.StatusCode}.");
    }

    return await response.Content.ReadFromJsonAsync<MockAadhaarTokenizeResponse>(cancellationToken)
      ?? throw new InvalidOperationException("Invalid mock tokenize response.");
  }
}
