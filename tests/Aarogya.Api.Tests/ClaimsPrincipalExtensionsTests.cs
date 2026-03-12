using System.Security.Claims;
using Aarogya.Api.Features.V1.Common;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class ClaimsPrincipalExtensionsTests
{
  [Fact]
  public void HasRole_ShouldReturnTrue_WhenRoleClaimPresent()
  {
    var principal = CreatePrincipal(ClaimTypes.Role, "Patient");

    principal.HasRole("Patient").Should().BeTrue();
  }

  [Fact]
  public void HasRole_ShouldReturnFalse_WhenRoleClaimMissing()
  {
    var principal = CreatePrincipal(ClaimTypes.Role, "Doctor");

    principal.HasRole("Patient").Should().BeFalse();
  }

  [Fact]
  public void HasRole_ShouldReturnFalse_WhenNoClaims()
  {
    var principal = new ClaimsPrincipal(new ClaimsIdentity());

    principal.HasRole("Patient").Should().BeFalse();
  }

  [Fact]
  public void HasRole_ShouldNotMatchCognitoGroupsClaim()
  {
    // Verifies HasRole checks ClaimTypes.Role, not cognito:groups
    var principal = CreatePrincipal("cognito:groups", "Patient");

    principal.HasRole("Patient").Should().BeFalse();
  }

  [Fact]
  public void GetSubjectOrNull_ShouldReturnSub_WhenPresent()
  {
    var principal = CreatePrincipal("sub", "user-123");

    principal.GetSubjectOrNull().Should().Be("user-123");
  }

  [Fact]
  public void GetSubjectOrNull_ShouldReturnNull_WhenMissing()
  {
    var principal = new ClaimsPrincipal(new ClaimsIdentity());

    principal.GetSubjectOrNull().Should().BeNull();
  }

  private static ClaimsPrincipal CreatePrincipal(string claimType, string claimValue)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(
      [new Claim(claimType, claimValue)],
      "TestAuth"));
  }
}
