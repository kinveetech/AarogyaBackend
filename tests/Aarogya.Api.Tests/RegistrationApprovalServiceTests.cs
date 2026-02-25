using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class RegistrationApprovalServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 25, 10, 0, 0, TimeSpan.Zero);

  #region ApproveAsync

  [Fact]
  public async Task ApproveAsync_ShouldSetStatusToApprovedAsync()
  {
    var (service, mocks) = CreateService();
    var admin = CreateUser("admin-sub", UserRole.Admin, RegistrationStatus.Approved);
    var target = CreateUser("target-sub", UserRole.Doctor, RegistrationStatus.PendingApproval);

    SetupUserLookup(mocks, "admin-sub", admin);
    SetupUserLookup(mocks, "target-sub", target);

    var result = await service.ApproveAsync(
      "admin-sub", "target-sub",
      new ApproveRegistrationRequest("Looks good"),
      CancellationToken.None);

    result.RegistrationStatus.Should().Be("approved");
    result.Sub.Should().Be("target-sub");
    result.Role.Should().Be("Doctor");

    target.RegistrationStatus.Should().Be(RegistrationStatus.Approved);
    target.IsActive.Should().BeTrue();
    target.UpdatedAt.Should().Be(FixedNow);

    mocks.UserRepo.Verify(x => x.Update(target), Times.Once);
    mocks.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task ApproveAsync_ShouldThrow_WhenAdminNotFoundAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("missing-admin", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var action = async () => await service.ApproveAsync(
      "missing-admin", "target-sub",
      new ApproveRegistrationRequest(null),
      CancellationToken.None);

    await action.Should().ThrowAsync<KeyNotFoundException>()
      .WithMessage("*Admin user not found*");
  }

  [Fact]
  public async Task ApproveAsync_ShouldThrow_WhenTargetNotFoundAsync()
  {
    var (service, mocks) = CreateService();
    var admin = CreateUser("admin-sub", UserRole.Admin, RegistrationStatus.Approved);
    SetupUserLookup(mocks, "admin-sub", admin);
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("missing-target", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var action = async () => await service.ApproveAsync(
      "admin-sub", "missing-target",
      new ApproveRegistrationRequest(null),
      CancellationToken.None);

    await action.Should().ThrowAsync<KeyNotFoundException>()
      .WithMessage("*not found*");
  }

  [Fact]
  public async Task ApproveAsync_ShouldThrow_WhenStatusNotPendingAsync()
  {
    var (service, mocks) = CreateService();
    var admin = CreateUser("admin-sub", UserRole.Admin, RegistrationStatus.Approved);
    var target = CreateUser("target-sub", UserRole.Doctor, RegistrationStatus.Approved);
    SetupUserLookup(mocks, "admin-sub", admin);
    SetupUserLookup(mocks, "target-sub", target);

    var action = async () => await service.ApproveAsync(
      "admin-sub", "target-sub",
      new ApproveRegistrationRequest(null),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*Cannot approve*");
  }

  #endregion

  #region RejectAsync

  [Fact]
  public async Task RejectAsync_ShouldSetStatusToRejectedAsync()
  {
    var (service, mocks) = CreateService();
    var admin = CreateUser("admin-sub", UserRole.Admin, RegistrationStatus.Approved);
    var target = CreateUser("target-sub", UserRole.LabTechnician, RegistrationStatus.PendingApproval);

    SetupUserLookup(mocks, "admin-sub", admin);
    SetupUserLookup(mocks, "target-sub", target);

    var result = await service.RejectAsync(
      "admin-sub", "target-sub",
      new RejectRegistrationRequest("Missing credentials"),
      CancellationToken.None);

    result.RegistrationStatus.Should().Be("rejected");
    result.RejectionReason.Should().Be("Missing credentials");
    result.Role.Should().Be("LabTechnician");

    target.RegistrationStatus.Should().Be(RegistrationStatus.Rejected);
    target.IsActive.Should().BeFalse();

    mocks.UserRepo.Verify(x => x.Update(target), Times.Once);
    mocks.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task RejectAsync_ShouldThrow_WhenTargetNotFoundAsync()
  {
    var (service, mocks) = CreateService();
    var admin = CreateUser("admin-sub", UserRole.Admin, RegistrationStatus.Approved);
    SetupUserLookup(mocks, "admin-sub", admin);
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("missing-target", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var action = async () => await service.RejectAsync(
      "admin-sub", "missing-target",
      new RejectRegistrationRequest("Not valid"),
      CancellationToken.None);

    await action.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task RejectAsync_ShouldThrow_WhenStatusNotPendingAsync()
  {
    var (service, mocks) = CreateService();
    var admin = CreateUser("admin-sub", UserRole.Admin, RegistrationStatus.Approved);
    var target = CreateUser("target-sub", UserRole.Doctor, RegistrationStatus.Rejected);
    SetupUserLookup(mocks, "admin-sub", admin);
    SetupUserLookup(mocks, "target-sub", target);

    var action = async () => await service.RejectAsync(
      "admin-sub", "target-sub",
      new RejectRegistrationRequest("Already handled"),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*Cannot reject*");
  }

  #endregion

  #region ListPendingAsync

  [Fact]
  public async Task ListPendingAsync_ShouldReturnPendingUsers_WithDoctorProfileAsync()
  {
    var (service, mocks) = CreateService();
    var doctor = CreateUser("pending-doctor", UserRole.Doctor, RegistrationStatus.PendingApproval);

    mocks.UserRepo
      .Setup(x => x.ListAsync(It.IsAny<UsersByRegistrationStatusSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new List<User> { doctor });

    mocks.DoctorRepo
      .Setup(x => x.GetByUserIdAsync(doctor.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DoctorProfile
      {
        Id = Guid.NewGuid(),
        UserId = doctor.Id,
        MedicalLicenseNumber = "MED-001",
        Specialization = "Dermatology"
      });

    var results = await service.ListPendingAsync(CancellationToken.None);

    results.Should().HaveCount(1);
    results[0].Sub.Should().Be("pending-doctor");
    results[0].Role.Should().Be("Doctor");
    results[0].DoctorData.Should().NotBeNull();
    results[0].DoctorData!.MedicalLicenseNumber.Should().Be("MED-001");
    results[0].LabTechnicianData.Should().BeNull();
  }

  [Fact]
  public async Task ListPendingAsync_ShouldReturnPendingUsers_WithLabProfileAsync()
  {
    var (service, mocks) = CreateService();
    var labTech = CreateUser("pending-lab", UserRole.LabTechnician, RegistrationStatus.PendingApproval);

    mocks.UserRepo
      .Setup(x => x.ListAsync(It.IsAny<UsersByRegistrationStatusSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new List<User> { labTech });

    mocks.LabTechRepo
      .Setup(x => x.GetByUserIdAsync(labTech.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new LabTechnicianProfile
      {
        Id = Guid.NewGuid(),
        UserId = labTech.Id,
        LabName = "City Lab"
      });

    var results = await service.ListPendingAsync(CancellationToken.None);

    results.Should().HaveCount(1);
    results[0].LabTechnicianData.Should().NotBeNull();
    results[0].LabTechnicianData!.LabName.Should().Be("City Lab");
    results[0].DoctorData.Should().BeNull();
  }

  [Fact]
  public async Task ListPendingAsync_ShouldReturnEmpty_WhenNoPendingUsersAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.ListAsync(It.IsAny<UsersByRegistrationStatusSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new List<User>());

    var results = await service.ListPendingAsync(CancellationToken.None);

    results.Should().BeEmpty();
  }

  #endregion

  #region Helpers

  private static User CreateUser(string sub, UserRole role, RegistrationStatus status)
  {
    return new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = sub,
      Role = role,
      RegistrationStatus = status,
      FirstName = "Test",
      LastName = role.ToString(),
      Email = $"{sub}@aarogya.dev",
      CreatedAt = FixedNow.AddDays(-1),
      UpdatedAt = FixedNow.AddDays(-1)
    };
  }

  private static void SetupUserLookup(ServiceMocks mocks, string sub, User user)
  {
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync(sub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(user);
  }

  private static (RegistrationApprovalService Service, ServiceMocks Mocks) CreateService()
  {
    var mocks = new ServiceMocks();
    var service = new RegistrationApprovalService(
      mocks.UserRepo.Object,
      mocks.DoctorRepo.Object,
      mocks.LabTechRepo.Object,
      mocks.UnitOfWork.Object,
      mocks.AuditService.Object,
      new FixedUtcClock(FixedNow));

    return (service, mocks);
  }

  private sealed record ServiceMocks
  {
    public Mock<IUserRepository> UserRepo { get; } = new();
    public Mock<IDoctorProfileRepository> DoctorRepo { get; } = new();
    public Mock<ILabTechnicianProfileRepository> LabTechRepo { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public Mock<IAuditLoggingService> AuditService { get; } = new();
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }

  #endregion
}
