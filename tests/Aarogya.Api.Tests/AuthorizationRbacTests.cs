using System.Security.Claims;
using Aarogya.Api.Authorization;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class AuthorizationRbacTests
{
  [Fact]
  public async Task ClaimsTransformation_ShouldMapCognitoGroupsToRoleClaimsAsync()
  {
    var principal = BuildPrincipal(
      new Claim("sub", "user-1"),
      new Claim("cognito:groups", "Doctor"));

    var transformer = new AarogyaRoleClaimsTransformation();
    var transformed = await transformer.TransformAsync(principal);

    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Doctor);
  }

  [Fact]
  public async Task ClaimsTransformation_ShouldExpandAdminHierarchyAsync()
  {
    var principal = BuildPrincipal(
      new Claim("sub", "admin-1"),
      new Claim("cognito:groups", "Admin"));

    var transformer = new AarogyaRoleClaimsTransformation();
    var transformed = await transformer.TransformAsync(principal);

    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Admin);
    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Patient);
    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Doctor);
    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.LabTechnician);
  }

  [Fact]
  public void RoleAssignment_ShouldRejectSelfEscalation()
  {
    var service = new InMemoryRoleAssignmentService();

    var success = service.TryAssignRole(
      actorSub: "user-123",
      actorRoles: [AarogyaRoles.Admin],
      targetSub: "user-123",
      targetRole: AarogyaRoles.Doctor,
      out var message);

    success.Should().BeFalse();
    message.Should().Contain("Cannot escalate");
  }

  [Fact]
  public void RoleAssignment_ShouldAllowAdminAssigningOtherUserRole()
  {
    var service = new InMemoryRoleAssignmentService();

    var success = service.TryAssignRole(
      actorSub: "admin-1",
      actorRoles: [AarogyaRoles.Admin],
      targetSub: "user-456",
      targetRole: AarogyaRoles.Doctor,
      out var message);

    success.Should().BeTrue();
    message.Should().Contain("assigned");
  }

  private static ClaimsPrincipal BuildPrincipal(params Claim[] claims)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
  }
}
