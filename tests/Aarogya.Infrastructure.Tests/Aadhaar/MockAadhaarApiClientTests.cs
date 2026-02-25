using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Aarogya.Infrastructure.Aadhaar;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Infrastructure.Tests.Aadhaar;

public sealed class MockAadhaarApiClientTests
{
  [Fact]
  public async Task ValidateAsync_ShouldReturnInvalid_WhenAadhaarNumberIsEmptyAsync()
  {
    var sut = CreateClient(useMockApi: false);

    var result = await sut.ValidateAsync("", cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeFalse();
    result.Message.Should().Be("Aadhaar number is required.");
    result.RequestId.Should().BeNull();
  }

  [Fact]
  public async Task ValidateAsync_ShouldReturnInvalid_WhenAadhaarNumberIsWhitespaceAsync()
  {
    var sut = CreateClient(useMockApi: false);

    var result = await sut.ValidateAsync("   ", cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public async Task ValidateAsync_ShouldReturnLocalValidation_WhenMockApiDisabledAsync()
  {
    var sut = CreateClient(useMockApi: false);

    var result = await sut.ValidateAsync("123456789012", cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeTrue();
    result.RequestId.Should().StartWith("local-");
    result.Provider.Should().Be("LOCAL");
    result.Demographics.Should().NotBeNull();
    result.Demographics!.FullName.Should().Contain("9012");
    result.Demographics.Address.Should().Be("India");
  }

  [Fact]
  public async Task ValidateAsync_ShouldReturnFallback_WhenMockApiThrowsHttpRequestExceptionAsync()
  {
    var handler = new FakeHttpMessageHandler(
      _ => throw new HttpRequestException("Connection refused"));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5099") };

    var sut = CreateClient(useMockApi: true, httpClient: httpClient);

    var result = await sut.ValidateAsync("123456789012", cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeTrue();
    result.RequestId.Should().StartWith("local-");
    result.Provider.Should().Be("LOCAL");
    result.Message.Should().Contain("Fallback");
  }

  [Fact]
  public async Task ValidateAsync_ShouldReturnApiResponse_WhenMockApiSucceedsAsync()
  {
    var expected = new MockAadhaarValidationResponse(
      true,
      "req-123",
      "Success",
      "MOCK",
      new MockAadhaarDemographics("Test User", new DateOnly(1990, 1, 1), "Male", "Mumbai"));

    var handler = new FakeHttpMessageHandler(_ =>
      new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = JsonContent.Create(expected)
      });
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5099") };

    var sut = CreateClient(useMockApi: true, httpClient: httpClient);

    var result = await sut.ValidateAsync("123456789012", cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeTrue();
    result.RequestId.Should().Be("req-123");
    result.Provider.Should().Be("MOCK");
    result.Demographics!.FullName.Should().Be("Test User");
  }

  [Fact]
  public async Task ValidateAsync_ShouldReturnInvalid_WhenMockApiReturnsNonSuccessStatusAsync()
  {
    var handler = new FakeHttpMessageHandler(_ =>
      new HttpResponseMessage(HttpStatusCode.InternalServerError));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5099") };

    var sut = CreateClient(useMockApi: true, httpClient: httpClient);

    var result = await sut.ValidateAsync("123456789012", cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeFalse();
    result.Message.Should().Contain("InternalServerError");
  }

  [Fact]
  public async Task ValidateAsync_ShouldUseDemographics_WhenProvidedAndMockApiDisabledAsync()
  {
    var sut = CreateClient(useMockApi: false);

    var result = await sut.ValidateAsync(
      "123456789012",
      firstName: "Ravi",
      lastName: "Kumar",
      dateOfBirth: new DateOnly(1990, 5, 15),
      cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeTrue();
    result.Demographics.Should().NotBeNull();
    result.Demographics!.FullName.Should().Be("Ravi Kumar");
    result.Demographics.DateOfBirth.Should().Be(new DateOnly(1990, 5, 15));
    result.Demographics.Address.Should().Be("India");
  }

  [Fact]
  public async Task ValidateAsync_ShouldFallBackToSuffix_WhenNoDemographicsProvidedAsync()
  {
    var sut = CreateClient(useMockApi: false);

    var result = await sut.ValidateAsync("123456789012", cancellationToken: CancellationToken.None);

    result.IsValid.Should().BeTrue();
    result.Demographics.Should().NotBeNull();
    result.Demographics!.FullName.Should().Contain("9012");
    result.Demographics.DateOfBirth.Should().BeNull();
  }

  [Fact]
  public async Task TokenizeAsync_ShouldThrow_WhenHashIsEmptyAsync()
  {
    var sut = CreateClient(useMockApi: false);

    var act = async () => await sut.TokenizeAsync([], CancellationToken.None);
    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task TokenizeAsync_ShouldReturnDeterministicToken_WhenMockApiDisabledAsync()
  {
    var sut = CreateClient(useMockApi: false);
    var hash = new byte[32];
    RandomNumberGenerator.Fill(hash);

    var result1 = await sut.TokenizeAsync(hash, CancellationToken.None);
    var result2 = await sut.TokenizeAsync(hash, CancellationToken.None);

    result1.ReferenceToken.Should().Be(result2.ReferenceToken);
    result1.RequestId.Should().BeNull();
  }

  [Fact]
  public async Task TokenizeAsync_ShouldReturnApiResponse_WhenMockApiSucceedsAsync()
  {
    var token = Guid.NewGuid();
    var expected = new MockAadhaarTokenizeResponse(token, "req-456");

    var handler = new FakeHttpMessageHandler(_ =>
      new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = JsonContent.Create(expected)
      });
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5099") };

    var sut = CreateClient(useMockApi: true, httpClient: httpClient);
    var hash = new byte[32];
    RandomNumberGenerator.Fill(hash);

    var result = await sut.TokenizeAsync(hash, CancellationToken.None);

    result.ReferenceToken.Should().Be(token);
    result.RequestId.Should().Be("req-456");
  }

  [Fact]
  public async Task TokenizeAsync_ShouldThrow_WhenMockApiReturnsNonSuccessAsync()
  {
    var handler = new FakeHttpMessageHandler(_ =>
      new HttpResponseMessage(HttpStatusCode.BadRequest));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5099") };

    var sut = CreateClient(useMockApi: true, httpClient: httpClient);
    var hash = new byte[32];
    RandomNumberGenerator.Fill(hash);

    var act = async () => await sut.TokenizeAsync(hash, CancellationToken.None);
    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task TokenizeAsync_ShouldReturnFallbackToken_WhenHttpRequestExceptionThrownAsync()
  {
    var handler = new FakeHttpMessageHandler(
      _ => throw new HttpRequestException("Connection refused"));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5099") };

    var sut = CreateClient(useMockApi: true, httpClient: httpClient);
    var hash = new byte[32];
    RandomNumberGenerator.Fill(hash);

    var result = await sut.TokenizeAsync(hash, CancellationToken.None);

    result.ReferenceToken.Should().NotBeEmpty();
    result.RequestId.Should().StartWith("local-");
  }

  private static MockAadhaarApiClient CreateClient(
    bool useMockApi,
    HttpClient? httpClient = null)
  {
    var options = Options.Create(new AadhaarVaultOptions
    {
      UseMockApi = useMockApi,
      MockApiBaseUrl = "http://localhost:5099",
      ValidateEndpoint = "/api/mock/uidai/validate",
      TokenizeEndpoint = "/api/mock/uidai/tokenize"
    });

    httpClient ??= new HttpClient { BaseAddress = new Uri("http://localhost:5099") };
    return new MockAadhaarApiClient(httpClient, options);
  }

  private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      return Task.FromResult(handler(request));
    }
  }
}
