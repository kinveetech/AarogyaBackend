using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class CognitoSocialAuthServiceTests
{
  [Fact]
  public async Task CreateAuthorizeUrlAsync_ShouldReturnHostedUiUrl_ForEnabledProviderAsync()
  {
    var service = CreateService();

    var result = await service.CreateAuthorizeUrlAsync(new SocialAuthorizeRequest(
      "google",
      new Uri("aarogya://auth/callback"),
      "state-123",
      "challenge",
      "S256"));

    result.Success.Should().BeTrue();
    result.AuthorizeUrl.Should().NotBeNull();
    result.AuthorizeUrl!.ToString().Should().Contain("identity_provider=Google");
    result.AuthorizeUrl.ToString().Should().Contain("redirect_uri=aarogya%3A%2F%2Fauth%2Fcallback");
    result.AuthorizeUrl.ToString().Should().Contain("aarogya-dev.auth.ap-south-1.amazoncognito.com");
    result.State.Should().Be("state-123");
  }

  [Fact]
  public async Task CreateAuthorizeUrlAsync_ShouldUseLocalStackUrl_WhenLocalStackEnabledAsync()
  {
    var awsOptions = CreateAwsOptions();
    awsOptions.UseLocalStack = true;
    awsOptions.ServiceUrl = "http://localhost:4566";
    var service = CreateService(awsOptions);

    var result = await service.CreateAuthorizeUrlAsync(new SocialAuthorizeRequest(
      "google",
      new Uri("aarogya://auth/callback"),
      "state-123",
      null,
      null));

    result.Success.Should().BeTrue();
    result.AuthorizeUrl!.ToString().Should().Contain("localhost:4566");
  }

  [Fact]
  public async Task ExchangeCodeAsync_ShouldPassThroughCognitoTokensAsync()
  {
    var service = CreateService();

    var result = await service.ExchangeCodeAsync(new SocialTokenRequest(
      "google",
      new Uri("aarogya://auth/callback"),
      "code-1",
      null));

    result.Success.Should().BeTrue();
    result.AccessToken.Should().Be("cognito-access-token");
    result.RefreshToken.Should().Be("cognito-refresh-token");
    result.IdToken.Should().Be("cognito-id-token");
    result.IsLinkedAccount.Should().BeFalse();
  }

  [Fact]
  public async Task ExchangeCodeAsync_ShouldFail_WhenProviderDisabledAsync()
  {
    var awsOptions = CreateAwsOptions();
    awsOptions.Cognito.SocialIdentityProviders.Apple.Enabled = false;

    var service = CreateService(awsOptions);
    var result = await service.ExchangeCodeAsync(new SocialTokenRequest(
      "apple",
      new Uri("aarogya://auth/callback"),
      "code",
      null));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("not enabled");
  }

  [Fact]
  public async Task ExchangeCodeAsync_ShouldFail_WhenCognitoExchangeFailsAsync()
  {
    var service = CreateService(tokenClient: new FailingCognitoSocialTokenClient());

    var result = await service.ExchangeCodeAsync(new SocialTokenRequest(
      "google",
      new Uri("aarogya://auth/callback"),
      "invalid-code",
      null));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("exchange");
  }

  [Fact]
  public async Task ExchangeCodeAsync_ShouldFail_WhenRedirectUriNotAllowedAsync()
  {
    var service = CreateService();

    var result = await service.ExchangeCodeAsync(new SocialTokenRequest(
      "google",
      new Uri("https://evil.com/callback"),
      "code-1",
      null));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Redirect URI");
  }

  [Fact]
  public async Task ExchangeCodeAsync_ShouldFail_WhenAuthorizationCodeEmptyAsync()
  {
    var service = CreateService();

    var result = await service.ExchangeCodeAsync(new SocialTokenRequest(
      "google",
      new Uri("aarogya://auth/callback"),
      "",
      null));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Authorization code");
  }

  [Fact]
  public async Task CreateAuthorizeUrlAsync_ShouldFail_WhenProviderUnsupportedAsync()
  {
    var service = CreateService();

    var result = await service.CreateAuthorizeUrlAsync(new SocialAuthorizeRequest(
      "twitter",
      new Uri("aarogya://auth/callback"),
      null,
      null,
      null));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Unsupported");
  }

  private static CognitoSocialAuthService CreateService(
    AwsOptions? awsOptions = null,
    ICognitoSocialTokenClient? tokenClient = null)
  {
    return new CognitoSocialAuthService(
      Options.Create(awsOptions ?? CreateAwsOptions()),
      tokenClient ?? new FakeCognitoSocialTokenClient());
  }

  private static AwsOptions CreateAwsOptions()
  {
    return new AwsOptions
    {
      Region = "ap-south-1",
      Cognito = new CognitoOptions
      {
        UserPoolName = "aarogya-dev-users",
        UserPoolId = "ap-south-1_poolId",
        AppClientId = "app-client-id",
        Issuer = "https://cognito-idp.ap-south-1.amazonaws.com/ap-south-1_poolId",
        Domain = "aarogya-dev",
        SocialIdentityProviders = new CognitoSocialIdentityProviderOptions
        {
          Google = CreateEnabledProvider("google-client-id", "google-client-secret"),
          Apple = CreateEnabledProvider("apple-client-id", "apple-client-secret"),
          Facebook = CreateEnabledProvider("facebook-client-id", "facebook-client-secret"),
          MobileRedirectUris = ["aarogya://auth/callback"]
        }
      }
    };
  }

  private static SocialProviderOptions CreateEnabledProvider(string clientId, string clientSecret)
  {
    return new SocialProviderOptions
    {
      Enabled = true,
      ClientId = clientId,
      ClientSecret = clientSecret
    };
  }

  private sealed class FakeCognitoSocialTokenClient : ICognitoSocialTokenClient
  {
    public Task<CognitoSocialTokenExchangeResult> ExchangeAuthorizationCodeAsync(
      string provider,
      Uri redirectUri,
      string authorizationCode,
      string? codeVerifier,
      CancellationToken cancellationToken = default)
    {
      var identity = new SocialIdentityClaims("google-user-1", "user@example.com", "Test", "User");

      return Task.FromResult(new CognitoSocialTokenExchangeResult(
        true,
        "ok",
        identity,
        3600,
        "Bearer",
        "cognito-access-token",
        "cognito-id-token",
        "cognito-refresh-token"));
    }
  }

  private sealed class FailingCognitoSocialTokenClient : ICognitoSocialTokenClient
  {
    public Task<CognitoSocialTokenExchangeResult> ExchangeAuthorizationCodeAsync(
      string provider,
      Uri redirectUri,
      string authorizationCode,
      string? codeVerifier,
      CancellationToken cancellationToken = default)
    {
      return Task.FromResult(new CognitoSocialTokenExchangeResult(false, "Failed to exchange authorization code."));
    }
  }
}
