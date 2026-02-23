using Aarogya.Api.Authorization;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Authorization;

public sealed class RoleAssignmentServiceTests
{
  private readonly Mock<IUserRepository> _userRepositoryMock = new();
  private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
  private readonly DatabaseRoleAssignmentService _service;

  public RoleAssignmentServiceTests()
  {
    _service = new DatabaseRoleAssignmentService(
      _userRepositoryMock.Object,
      _unitOfWorkMock.Object);
  }

  [Fact]
  public async Task TryAssignRoleAsync_ShouldReject_WhenActorIsNotAdminAsync()
  {
    var (success, message) = await _service.TryAssignRoleAsync(
      actorSub: "actor-1",
      actorRoles: ["Patient"],
      targetSub: "target-1",
      targetRole: "Doctor");

    success.Should().BeFalse();
    message.Should().Be("Only admins can assign roles.");
  }

  [Fact]
  public async Task TryAssignRoleAsync_ShouldReject_WhenTargetRoleIsUnknownAsync()
  {
    var (success, message) = await _service.TryAssignRoleAsync(
      actorSub: "actor-1",
      actorRoles: ["Admin"],
      targetSub: "target-1",
      targetRole: "SuperUser");

    success.Should().BeFalse();
    message.Should().Be("Unknown role.");
  }

  [Fact]
  public async Task TryAssignRoleAsync_ShouldReject_WhenSelfEscalationAsync()
  {
    var (success, message) = await _service.TryAssignRoleAsync(
      actorSub: "actor-1",
      actorRoles: ["Admin"],
      targetSub: "actor-1",
      targetRole: "Doctor");

    success.Should().BeFalse();
    message.Should().Be("Cannot escalate your own role.");
  }

  [Fact]
  public async Task TryAssignRoleAsync_ShouldReject_WhenUserNotFoundAsync()
  {
    _userRepositoryMock
      .Setup(r => r.GetByExternalAuthIdAsync("target-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var (success, message) = await _service.TryAssignRoleAsync(
      actorSub: "admin-1",
      actorRoles: ["Admin"],
      targetSub: "target-1",
      targetRole: "Doctor");

    success.Should().BeFalse();
    message.Should().Contain("not found");
  }

  [Fact]
  public async Task TryAssignRoleAsync_ShouldSucceed_WhenAdminAssignsValidRoleAsync()
  {
    var user = new User { ExternalAuthId = "target-1", Role = UserRole.Patient };
    _userRepositoryMock
      .Setup(r => r.GetByExternalAuthIdAsync("target-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var (success, message) = await _service.TryAssignRoleAsync(
      actorSub: "admin-1",
      actorRoles: ["Admin"],
      targetSub: "target-1",
      targetRole: "Doctor");

    success.Should().BeTrue();
    message.Should().Contain("Doctor");
    message.Should().Contain("target-1");
    user.Role.Should().Be(UserRole.Doctor);
    _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task TryAssignRoleAsync_ShouldSucceed_WhenAdminReassignsSelfExistingRoleAsync()
  {
    var user = new User { ExternalAuthId = "admin-1", Role = UserRole.Admin };
    _userRepositoryMock
      .Setup(r => r.GetByExternalAuthIdAsync("admin-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var (success, message) = await _service.TryAssignRoleAsync(
      actorSub: "admin-1",
      actorRoles: ["Admin"],
      targetSub: "admin-1",
      targetRole: "Admin");

    success.Should().BeTrue();
    message.Should().Contain("Admin");
  }

  [Fact]
  public async Task GetAssignedRolesAsync_ShouldReturnEmpty_ForUnknownUserAsync()
  {
    _userRepositoryMock
      .Setup(r => r.GetByExternalAuthIdAsync("nonexistent-user", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var roles = await _service.GetAssignedRolesAsync("nonexistent-user");
    roles.Should().BeEmpty();
  }

  [Fact]
  public async Task GetAssignedRolesAsync_ShouldReturnEmpty_ForEmptyUserSubAsync()
  {
    var roles = await _service.GetAssignedRolesAsync("");
    roles.Should().BeEmpty();
  }

  [Fact]
  public async Task GetAssignedRolesAsync_ShouldReturnUserRole_WhenUserExistsAsync()
  {
    var user = new User { ExternalAuthId = "target-1", Role = UserRole.Doctor };
    _userRepositoryMock
      .Setup(r => r.GetByExternalAuthIdAsync("target-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);

    var roles = await _service.GetAssignedRolesAsync("target-1");
    roles.Should().HaveCount(1);
    roles.Should().Contain("Doctor");
  }
}
