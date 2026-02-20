using System.IdentityModel.Tokens.Jwt;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class InMemorySocialAuthServiceTests
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
    result.State.Should().Be("state-123");
  }

  [Fact]
  public async Task ExchangeCodeAsync_ShouldLinkAccounts_WhenEmailMatchesAcrossProvidersAsync()
  {
    var service = CreateService();

    var google = await service.ExchangeCodeAsync(new SocialTokenRequest(
      "google",
      new Uri("aarogya://auth/callback"),
      "code-1",
      "google-user-1",
      "same.user@example.com",
      "Same",
      "User"));

    var facebook = await service.ExchangeCodeAsync(new SocialTokenRequest(
      "facebook",
      new Uri("aarogya://auth/callback"),
      "code-2",
      "facebook-user-9",
      "same.user@example.com",
      "Same",
      "User"));

    google.Success.Should().BeTrue();
    facebook.Success.Should().BeTrue();
    facebook.IsLinkedAccount.Should().BeTrue();

    var googleJwt = new JwtSecurityTokenHandler().ReadJwtToken(google.AccessToken);
    var facebookJwt = new JwtSecurityTokenHandler().ReadJwtToken(facebook.AccessToken);
    googleJwt.Subject.Should().Be(facebookJwt.Subject);
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
      "apple-sub",
      "apple.user@example.com",
      "Apple",
      "User"));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("not enabled");
  }

  private static InMemorySocialAuthService CreateService(AwsOptions? awsOptions = null)
  {
    return new InMemorySocialAuthService(
      Options.Create(awsOptions ?? CreateAwsOptions()),
      Options.Create(new PkceOptions
      {
        AuthorizationCodeExpirySeconds = 300,
        AccessTokenExpirySeconds = 900,
        RefreshTokenExpirySeconds = 2592000
      }),
      Options.Create(new JwtOptions
      {
        Key = "test-signing-key-12345678901234567890",
        Issuer = "AarogyaAPI",
        Audience = "AarogyaClients"
      }),
      new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero)));
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

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
  }
}
