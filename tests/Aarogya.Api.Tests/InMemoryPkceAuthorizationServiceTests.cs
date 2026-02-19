using System.Security.Cryptography;
using System.Text;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class InMemoryPkceAuthorizationServiceTests
{
  [Fact]
  public async Task CreateAuthorizationCodeAsync_ShouldSucceed_ForValidRequestAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock);
    var codeVerifier = BuildCodeVerifier();
    var codeChallenge = ComputeCodeChallenge(codeVerifier);

    var result = await service.CreateAuthorizationCodeAsync(new PkceAuthorizeRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      codeChallenge,
      "S256",
      "ios",
      "openid profile email",
      "state-123"));

    result.Success.Should().BeTrue();
    result.AuthorizationCode.Should().NotBeNullOrWhiteSpace();
    result.ExpiresAt.Should().NotBeNull();
  }

  [Fact]
  public async Task ExchangeAuthorizationCodeAsync_ShouldIssueTokens_WhenVerifierMatchesAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock);
    var codeVerifier = BuildCodeVerifier();
    var codeChallenge = ComputeCodeChallenge(codeVerifier);

    var authorize = await service.CreateAuthorizationCodeAsync(new PkceAuthorizeRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      codeChallenge,
      "S256",
      "android",
      "openid",
      null));

    var exchange = await service.ExchangeAuthorizationCodeAsync(new PkceTokenRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      authorize.AuthorizationCode!,
      codeVerifier));

    exchange.Success.Should().BeTrue();
    exchange.AccessToken.Should().NotBeNullOrWhiteSpace();
    exchange.RefreshToken.Should().NotBeNullOrWhiteSpace();
    exchange.IdToken.Should().NotBeNullOrWhiteSpace();
    exchange.TokenType.Should().Be("Bearer");
  }

  [Fact]
  public async Task ExchangeAuthorizationCodeAsync_ShouldFail_ForInvalidVerifierAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock);
    var codeVerifier = BuildCodeVerifier();
    var codeChallenge = ComputeCodeChallenge(codeVerifier);

    var authorize = await service.CreateAuthorizationCodeAsync(new PkceAuthorizeRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      codeChallenge,
      "S256",
      "android",
      "openid",
      null));

    var invalidVerifier = new string('a', 43);
    var exchange = await service.ExchangeAuthorizationCodeAsync(new PkceTokenRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      authorize.AuthorizationCode!,
      invalidVerifier));

    exchange.Success.Should().BeFalse();
    exchange.Message.Should().Contain("Invalid PKCE code verifier");
  }

  [Fact]
  public async Task ExchangeAuthorizationCodeAsync_ShouldFail_WhenAuthorizationCodeExpiredAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock, new PkceOptions
    {
      AuthorizationCodeExpirySeconds = 30,
      AccessTokenExpirySeconds = 900,
      RefreshTokenExpirySeconds = 2592000
    });
    var codeVerifier = BuildCodeVerifier();
    var codeChallenge = ComputeCodeChallenge(codeVerifier);

    var authorize = await service.CreateAuthorizationCodeAsync(new PkceAuthorizeRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      codeChallenge,
      "S256",
      "ios",
      "openid",
      null));

    clock.Advance(TimeSpan.FromMinutes(2));

    var exchange = await service.ExchangeAuthorizationCodeAsync(new PkceTokenRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      authorize.AuthorizationCode!,
      codeVerifier));

    exchange.Success.Should().BeFalse();
    exchange.Message.Should().MatchRegex("(?i).*expired.*");
  }

  [Fact]
  public async Task CreateAuthorizationCodeAsync_ShouldFail_WhenPlatformIsUnsupportedAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock);
    var codeVerifier = BuildCodeVerifier();
    var codeChallenge = ComputeCodeChallenge(codeVerifier);

    var authorize = await service.CreateAuthorizationCodeAsync(new PkceAuthorizeRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      codeChallenge,
      "S256",
      "web",
      "openid",
      null));

    authorize.Success.Should().BeFalse();
    authorize.Message.Should().Contain("ios or android");
  }

  private static InMemoryPkceAuthorizationService CreateService(FakeClock clock, PkceOptions? pkceOptions = null)
  {
    return new InMemoryPkceAuthorizationService(
      Options.Create(pkceOptions ?? new PkceOptions()),
      Options.Create(new AwsOptions
      {
        Cognito = new CognitoOptions
        {
          AppClientId = "mobile-client-id",
          UserPoolId = "ap-south-1_pool",
          UserPoolName = "aarogya-dev-users"
        }
      }),
      clock);
  }

  private static string BuildCodeVerifier()
  {
    return new string('A', 64);
  }

  private static string ComputeCodeChallenge(string codeVerifier)
  {
    var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
    return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Advance(TimeSpan by)
    {
      UtcNow = UtcNow.Add(by);
    }
  }
}
