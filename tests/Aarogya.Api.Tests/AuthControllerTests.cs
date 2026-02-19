using System.Security.Claims;
using Aarogya.Api.Authentication;
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

    var controller = new AuthController(new NoopPhoneOtpService())
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
}
