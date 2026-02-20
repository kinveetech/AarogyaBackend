using System.Security.Claims;
using Aarogya.Api.Authentication;
using Aarogya.Api.Authorization;
using Aarogya.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class AuthControllerTests
{
  private static readonly string[] ExpectedRoles = ["Doctor", "Admin"];

  [Fact]
  public void GetCurrentUserClaims_ShouldExtractSubEmailAndRoles()
  {
    var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim("sub", "user-123"),
      new Claim("email", "user@example.com"),
      new Claim("cognito:groups", "Doctor"),
      new Claim("cognito:groups", "Admin")
    ], "TestAuth"));

    var controller = new AuthController(
      new NoopPhoneOtpService(),
      new NoopPkceAuthorizationService(),
      new NoopSocialAuthService(),
      new NoopRoleAssignmentService())
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext
        {
          User = claimsPrincipal
        }
      }
    };

    var result = controller.GetCurrentUserClaims();
    var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    okResult.Value.Should().NotBeNull();
    var payload = okResult.Value;

    payload.Should().BeEquivalentTo(new
    {
      Sub = "user-123",
      Email = "user@example.com",
      Roles = ExpectedRoles
    });
  }

  private sealed class NoopPhoneOtpService : IPhoneOtpService
  {
    public Task<OtpRequestResult> RequestOtpAsync(string phoneNumber, CancellationToken cancellationToken = default)
      => Task.FromResult(new OtpRequestResult(true, false, "ok"));

    public Task<OtpVerificationResult> VerifyOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default)
      => Task.FromResult(new OtpVerificationResult(true, "ok"));
  }

  private sealed class NoopPkceAuthorizationService : IPkceAuthorizationService
  {
    public Task<PkceAuthorizeResult> CreateAuthorizationCodeAsync(PkceAuthorizeRequest request, CancellationToken cancellationToken = default)
      => Task.FromResult(new PkceAuthorizeResult(true, "ok", "code", DateTimeOffset.UtcNow.AddMinutes(5)));

    public Task<PkceTokenResult> ExchangeAuthorizationCodeAsync(PkceTokenRequest request, CancellationToken cancellationToken = default)
      => Task.FromResult(new PkceTokenResult(true, "ok", "access", "refresh", "id", 900));

    public Task<PkceTokenResult> ExchangeRefreshTokenAsync(PkceRefreshTokenRequest request, CancellationToken cancellationToken = default)
      => Task.FromResult(new PkceTokenResult(true, "ok", "access", "refresh", "id", 900));

    public Task<PkceRevokeResult> RevokeRefreshTokenAsync(PkceRevokeRequest request, CancellationToken cancellationToken = default)
      => Task.FromResult(new PkceRevokeResult(true, "ok"));
  }

  private sealed class NoopRoleAssignmentService : IRoleAssignmentService
  {
    public bool TryAssignRole(string actorSub, IReadOnlyCollection<string> actorRoles, string targetSub, string targetRole, out string message)
    {
      message = "ok";
      return true;
    }

    public IReadOnlyCollection<string> GetAssignedRoles(string userSub)
      => [];
  }

  private sealed class NoopSocialAuthService : ISocialAuthService
  {
    public Task<SocialAuthorizeResult> CreateAuthorizeUrlAsync(SocialAuthorizeRequest request, CancellationToken cancellationToken = default)
      => Task.FromResult(new SocialAuthorizeResult(true, "ok", new Uri("https://example.com/authorize"), "state"));

    public Task<SocialTokenResult> ExchangeCodeAsync(SocialTokenRequest request, CancellationToken cancellationToken = default)
      => Task.FromResult(new SocialTokenResult(true, "ok", "access", "refresh", "id", 900, "Bearer", false));
  }
}
