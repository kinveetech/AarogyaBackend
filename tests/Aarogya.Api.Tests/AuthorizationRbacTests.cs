using System.Security.Claims;
using Aarogya.Api.Authorization;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class AuthorizationRbacTests
{
  private readonly Mock<IRoleAssignmentService> _roleAssignmentMock = new();

  [Fact]
  public async Task ClaimsTransformation_ShouldMapCognitoGroupsToRoleClaimsAsync()
  {
    _roleAssignmentMock
      .Setup(s => s.GetAssignedRolesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<string>());

    var principal = BuildPrincipal(
      new Claim("sub", "user-1"),
      new Claim("cognito:groups", "Doctor"));

    var transformer = new AarogyaRoleClaimsTransformation(_roleAssignmentMock.Object);
    var transformed = await transformer.TransformAsync(principal);

    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Doctor);
  }

  [Fact]
  public async Task ClaimsTransformation_ShouldExpandAdminHierarchyAsync()
  {
    _roleAssignmentMock
      .Setup(s => s.GetAssignedRolesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<string>());

    var principal = BuildPrincipal(
      new Claim("sub", "admin-1"),
      new Claim("cognito:groups", "Admin"));

    var transformer = new AarogyaRoleClaimsTransformation(_roleAssignmentMock.Object);
    var transformed = await transformer.TransformAsync(principal);

    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Admin);
    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Patient);
    transformed.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.Doctor);
    transformed.Claims.Should()
      .Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == AarogyaRoles.LabTechnician);
  }

  [Fact]
  public async Task RoleAssignment_ShouldRejectSelfEscalationAsync()
  {
    var userRepoMock = new Mock<IUserRepository>();
    var unitOfWorkMock = new Mock<IUnitOfWork>();
    var service = new DatabaseRoleAssignmentService(userRepoMock.Object, unitOfWorkMock.Object);

    var (success, message) = await service.TryAssignRoleAsync(
      actorSub: "user-123",
      actorRoles: [AarogyaRoles.Admin],
      targetSub: "user-123",
      targetRole: AarogyaRoles.Doctor);

    success.Should().BeFalse();
    message.Should().Contain("Cannot escalate");
  }

  [Fact]
  public async Task RoleAssignment_ShouldAllowAdminAssigningOtherUserRoleAsync()
  {
    var userRepoMock = new Mock<IUserRepository>();
    var unitOfWorkMock = new Mock<IUnitOfWork>();
    var user = new User { ExternalAuthId = "user-456", Role = UserRole.Patient };
    userRepoMock
      .Setup(r => r.GetByExternalAuthIdAsync("user-456", It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);
    var service = new DatabaseRoleAssignmentService(userRepoMock.Object, unitOfWorkMock.Object);

    var (success, message) = await service.TryAssignRoleAsync(
      actorSub: "admin-1",
      actorRoles: [AarogyaRoles.Admin],
      targetSub: "user-456",
      targetRole: AarogyaRoles.Doctor);

    success.Should().BeTrue();
    message.Should().Contain("assigned");
    user.Role.Should().Be(UserRole.Doctor);

    var roles = await service.GetAssignedRolesAsync("user-456");
    roles.Should().Contain(AarogyaRoles.Doctor);
  }

  private static ClaimsPrincipal BuildPrincipal(params Claim[] claims)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
  }
}
