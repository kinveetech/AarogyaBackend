using Aarogya.Api.Authorization;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests.Authorization;

public sealed class RoleAssignmentServiceTests
{
  private readonly InMemoryRoleAssignmentService _service = new();

  [Fact]
  public void TryAssignRole_ShouldReject_WhenActorIsNotAdmin()
  {
    var success = _service.TryAssignRole(
      actorSub: "actor-1",
      actorRoles: ["Patient"],
      targetSub: "target-1",
      targetRole: "Doctor",
      out var message);

    success.Should().BeFalse();
    message.Should().Be("Only admins can assign roles.");
  }

  [Fact]
  public void TryAssignRole_ShouldReject_WhenTargetRoleIsUnknown()
  {
    var success = _service.TryAssignRole(
      actorSub: "actor-1",
      actorRoles: ["Admin"],
      targetSub: "target-1",
      targetRole: "SuperUser",
      out var message);

    success.Should().BeFalse();
    message.Should().Be("Unknown role.");
  }

  [Fact]
  public void TryAssignRole_ShouldReject_WhenSelfEscalation()
  {
    var success = _service.TryAssignRole(
      actorSub: "actor-1",
      actorRoles: ["Admin"],
      targetSub: "actor-1",
      targetRole: "Doctor",
      out var message);

    success.Should().BeFalse();
    message.Should().Be("Cannot escalate your own role.");
  }

  [Fact]
  public void TryAssignRole_ShouldSucceed_WhenAdminAssignsValidRole()
  {
    var success = _service.TryAssignRole(
      actorSub: "admin-1",
      actorRoles: ["Admin"],
      targetSub: "target-1",
      targetRole: "Doctor",
      out var message);

    success.Should().BeTrue();
    message.Should().Contain("Doctor");
    message.Should().Contain("target-1");
  }

  [Fact]
  public void TryAssignRole_ShouldSucceed_WhenAdminReassignsSelfExistingRole()
  {
    var success = _service.TryAssignRole(
      actorSub: "admin-1",
      actorRoles: ["Admin"],
      targetSub: "admin-1",
      targetRole: "Admin",
      out var message);

    success.Should().BeTrue();
    message.Should().Contain("Admin");
  }

  [Fact]
  public void GetAssignedRoles_ShouldReturnEmpty_ForUnknownUser()
  {
    var roles = _service.GetAssignedRoles("nonexistent-user");
    roles.Should().BeEmpty();
  }

  [Fact]
  public void GetAssignedRoles_ShouldReturnEmpty_ForEmptyUserSub()
  {
    var roles = _service.GetAssignedRoles("");
    roles.Should().BeEmpty();
  }

  [Fact]
  public void GetAssignedRoles_ShouldReturnAssignedRoles_AfterSuccessfulAssignment()
  {
    _service.TryAssignRole(
      actorSub: "admin-1",
      actorRoles: ["Admin"],
      targetSub: "target-1",
      targetRole: "Doctor",
      out _);

    _service.TryAssignRole(
      actorSub: "admin-1",
      actorRoles: ["Admin"],
      targetSub: "target-1",
      targetRole: "Patient",
      out _);

    var roles = _service.GetAssignedRoles("target-1");
    roles.Should().HaveCount(2);
    roles.Should().Contain("Doctor");
    roles.Should().Contain("Patient");
  }
}
