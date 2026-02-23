using System.Net;
using System.Text.Json;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class CognitoTokenManagementServiceTests
{
  [Fact]
  public async Task RefreshTokenAsync_ShouldReturnTokens_OnSuccessAsync()
  {
    var responsePayload = JsonSerializer.Serialize(new
    {
      access_token = "new-access-token",
      id_token = "new-id-token",
      expires_in = 3600,
      token_type = "Bearer"
    });

    var service = CreateService(HttpStatusCode.OK, responsePayload);

    var result = await service.RefreshTokenAsync("old-refresh-token");

    result.Success.Should().BeTrue();
    result.AccessToken.Should().Be("new-access-token");
    result.IdToken.Should().Be("new-id-token");
    result.RefreshToken.Should().Be("old-refresh-token");
    result.ExpiresInSeconds.Should().Be(3600);
  }

  [Fact]
  public async Task RefreshTokenAsync_ShouldFail_WhenCognitoReturnsErrorAsync()
  {
    var service = CreateService(HttpStatusCode.BadRequest, "{}");

    var result = await service.RefreshTokenAsync("bad-refresh-token");

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Failed to refresh");
  }

  [Fact]
  public async Task RefreshTokenAsync_ShouldFail_WhenRefreshTokenEmptyAsync()
  {
    var service = CreateService(HttpStatusCode.OK, "{}");

    var result = await service.RefreshTokenAsync("");

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Refresh token is required");
  }

  [Fact]
  public async Task RevokeTokenAsync_ShouldSucceed_OnCognitoSuccessAsync()
  {
    var service = CreateService(HttpStatusCode.OK, "");

    var (success, message) = await service.RevokeTokenAsync("some-refresh-token");

    success.Should().BeTrue();
    message.Should().Contain("revoked");
  }

  [Fact]
  public async Task RevokeTokenAsync_ShouldFail_WhenCognitoReturnsErrorAsync()
  {
    var service = CreateService(HttpStatusCode.BadRequest, "");

    var (success, message) = await service.RevokeTokenAsync("bad-refresh-token");

    success.Should().BeFalse();
    message.Should().Contain("Failed to revoke");
  }

  [Fact]
  public async Task RevokeTokenAsync_ShouldFail_WhenRefreshTokenEmptyAsync()
  {
    var service = CreateService(HttpStatusCode.OK, "");

    var (success, message) = await service.RevokeTokenAsync("");

    success.Should().BeFalse();
    message.Should().Contain("Refresh token is required");
  }

  private static CognitoTokenManagementService CreateService(HttpStatusCode statusCode, string responseContent)
  {
    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler.Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync(new HttpResponseMessage
      {
        StatusCode = statusCode,
        Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
      });

    var httpClient = new HttpClient(mockHandler.Object);
    var httpClientFactory = new Mock<IHttpClientFactory>();
    httpClientFactory
      .Setup(factory => factory.CreateClient(CognitoOAuthTokenClient.HttpClientName))
      .Returns(httpClient);

    var awsOptions = new AwsOptions
    {
      Region = "ap-south-1",
      Cognito = new CognitoOptions
      {
        UserPoolName = "aarogya-dev-users",
        UserPoolId = "ap-south-1_poolId",
        AppClientId = "app-client-id",
        Domain = "aarogya-dev"
      }
    };

    return new CognitoTokenManagementService(
      Options.Create(awsOptions),
      httpClientFactory.Object);
  }
}
