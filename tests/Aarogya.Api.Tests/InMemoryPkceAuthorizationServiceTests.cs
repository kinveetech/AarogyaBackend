using System.IdentityModel.Tokens.Jwt;
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
    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(exchange.AccessToken);
    jwt.Issuer.Should().Be("AarogyaAPI");
    jwt.Audiences.Should().ContainSingle("AarogyaClients");
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

  [Fact]
  public async Task CreateAuthorizationCodeAsync_ShouldFail_WhenS256ChallengeLengthIsNot43Async()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock);
    var invalidChallenge = new string('A', 44);

    var authorize = await service.CreateAuthorizationCodeAsync(new PkceAuthorizeRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      invalidChallenge,
      "S256",
      "ios",
      "openid",
      null));

    authorize.Success.Should().BeFalse();
    authorize.Message.Should().Contain("Code challenge is invalid");
  }

  [Fact]
  public async Task ExchangeRefreshTokenAsync_ShouldRotateRefreshToken_AndIssueNewAccessTokenAsync()
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
      "ios",
      "openid",
      null));

    var initialExchange = await service.ExchangeAuthorizationCodeAsync(new PkceTokenRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      authorize.AuthorizationCode!,
      codeVerifier));

    var rotated = await service.ExchangeRefreshTokenAsync(new PkceRefreshTokenRequest(
      "mobile-client-id",
      initialExchange.RefreshToken!));

    rotated.Success.Should().BeTrue();
    rotated.RefreshToken.Should().NotBe(initialExchange.RefreshToken);
    rotated.AccessToken.Should().NotBeNullOrWhiteSpace();

    var replay = await service.ExchangeRefreshTokenAsync(new PkceRefreshTokenRequest(
      "mobile-client-id",
      initialExchange.RefreshToken!));
    replay.Success.Should().BeFalse();
  }

  [Fact]
  public async Task ExchangeRefreshTokenAsync_ShouldRejectExpiredRefreshTokenAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock, new PkceOptions
    {
      AuthorizationCodeExpirySeconds = 300,
      AccessTokenExpirySeconds = 900,
      RefreshTokenExpirySeconds = 30
    });
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

    var initialExchange = await service.ExchangeAuthorizationCodeAsync(new PkceTokenRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      authorize.AuthorizationCode!,
      codeVerifier));

    clock.Advance(TimeSpan.FromMinutes(2));

    var refreshed = await service.ExchangeRefreshTokenAsync(new PkceRefreshTokenRequest(
      "mobile-client-id",
      initialExchange.RefreshToken!));

    refreshed.Success.Should().BeFalse();
    refreshed.Message.Should().Contain("expired");
  }

  [Fact]
  public async Task RevokeRefreshTokenAsync_ShouldPreventFurtherRefreshAsync()
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
      "ios",
      "openid",
      null));

    var initialExchange = await service.ExchangeAuthorizationCodeAsync(new PkceTokenRequest(
      "mobile-client-id",
      new Uri("myapp://auth/callback"),
      authorize.AuthorizationCode!,
      codeVerifier));

    var revoke = await service.RevokeRefreshTokenAsync(new PkceRevokeRequest(
      "mobile-client-id",
      initialExchange.RefreshToken!));
    revoke.Success.Should().BeTrue();
    revoke.Message.Should().Contain("revoked");

    var revokeAgain = await service.RevokeRefreshTokenAsync(new PkceRevokeRequest(
      "mobile-client-id",
      initialExchange.RefreshToken!));
    revokeAgain.Success.Should().BeTrue();

    var refreshed = await service.ExchangeRefreshTokenAsync(new PkceRefreshTokenRequest(
      "mobile-client-id",
      initialExchange.RefreshToken!));
    refreshed.Success.Should().BeFalse();
  }

  [Fact]
  public async Task RevokeRefreshTokenAsync_ShouldSucceed_WhenTokenDoesNotExistAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock);

    var revoke = await service.RevokeRefreshTokenAsync(new PkceRevokeRequest(
      "mobile-client-id",
      "missing-refresh-token"));

    revoke.Success.Should().BeTrue();
    revoke.Message.Should().Contain("revoked");
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
      Options.Create(new JwtOptions
      {
        Key = "test-signing-key-12345678901234567890",
        Issuer = "AarogyaAPI",
        Audience = "AarogyaClients"
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
