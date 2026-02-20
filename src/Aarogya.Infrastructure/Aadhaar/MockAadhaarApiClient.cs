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
      return new MockAadhaarValidationResponse(false, null, "Aadhaar number is required.", null, null);
    }

    if (!_options.UseMockApi)
    {
      return new MockAadhaarValidationResponse(
        true,
        $"local-{Guid.NewGuid():N}",
        "Mock API disabled.",
        "LOCAL",
        CreateFallbackDemographics(aadhaarNumber));
    }

    try
    {
      var request = new MockAadhaarValidationRequest(aadhaarNumber);
      var response = await httpClient.PostAsJsonAsync(_options.ValidateEndpoint, request, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        return new MockAadhaarValidationResponse(
          false,
          null,
          $"Mock Aadhaar validate API returned {response.StatusCode}.",
          null,
          null);
      }

      return await response.Content.ReadFromJsonAsync<MockAadhaarValidationResponse>(cancellationToken)
        ?? new MockAadhaarValidationResponse(false, null, "Invalid mock validation response.", null, null);
    }
    catch (HttpRequestException)
    {
      return new MockAadhaarValidationResponse(
        true,
        $"local-{Guid.NewGuid():N}",
        "Fallback local validation used.",
        "LOCAL",
        CreateFallbackDemographics(aadhaarNumber));
    }
  }

  public async Task<MockAadhaarTokenizeResponse> TokenizeAsync(byte[] aadhaarSha256, CancellationToken cancellationToken = default)
  {
    if (aadhaarSha256.Length == 0)
    {
      throw new ArgumentException("Aadhaar SHA-256 hash is required.", nameof(aadhaarSha256));
    }

    if (!_options.UseMockApi)
    {
      return new MockAadhaarTokenizeResponse(CreateDeterministicToken(aadhaarSha256), null);
    }

    try
    {
      var request = new MockAadhaarTokenizeRequest(Convert.ToBase64String(aadhaarSha256));
      var response = await httpClient.PostAsJsonAsync(_options.TokenizeEndpoint, request, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        throw new InvalidOperationException($"Mock Aadhaar tokenize API returned {response.StatusCode}.");
      }

      return await response.Content.ReadFromJsonAsync<MockAadhaarTokenizeResponse>(cancellationToken)
        ?? throw new InvalidOperationException("Invalid mock tokenize response.");
    }
    catch (HttpRequestException)
    {
      return new MockAadhaarTokenizeResponse(CreateDeterministicToken(aadhaarSha256), $"local-{Guid.NewGuid():N}");
    }
  }

  private static Guid CreateDeterministicToken(byte[] sha256Hash)
  {
    var tokenBytes = sha256Hash.Take(16).ToArray();
    return new Guid(tokenBytes);
  }

  private static MockAadhaarDemographics CreateFallbackDemographics(string normalizedAadhaar)
  {
    var suffix = normalizedAadhaar[^4..];
    return new MockAadhaarDemographics(
      $"Verified Holder {suffix}",
      null,
      null,
      "India");
  }
}
