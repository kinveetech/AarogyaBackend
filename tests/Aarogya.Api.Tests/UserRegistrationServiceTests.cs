using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class UserRegistrationServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 25, 10, 0, 0, TimeSpan.Zero);

  #region RegisterAsync — Patient

  [Fact]
  public async Task RegisterAsync_ShouldCreatePatient_WithApprovedStatusAsync()
  {
    var (service, mocks) = CreateService();

    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-patient", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = CreatePatientRequest();

    var result = await service.RegisterAsync("new-patient", request, CancellationToken.None);

    result.Sub.Should().Be("new-patient");
    result.Role.Should().Be("Patient");
    result.RegistrationStatus.Should().Be("approved");
    result.FirstName.Should().Be("Test");
    result.LastName.Should().Be("Patient");
    result.Email.Should().Be("test@aarogya.dev");

    mocks.UserRepo.Verify(x => x.AddAsync(It.Is<User>(u =>
      u.ExternalAuthId == "new-patient"
      && u.Role == UserRole.Patient
      && u.RegistrationStatus == RegistrationStatus.Approved
      && u.IsActive), It.IsAny<CancellationToken>()), Times.Once);

    mocks.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task RegisterAsync_ShouldSetPatientActiveImmediatelyAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-patient", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = CreatePatientRequest();
    await service.RegisterAsync("new-patient", request, CancellationToken.None);

    mocks.UserRepo.Verify(x => x.AddAsync(
      It.Is<User>(u => u.IsActive), It.IsAny<CancellationToken>()), Times.Once);
  }

  #endregion

  #region RegisterAsync — Doctor

  [Fact]
  public async Task RegisterAsync_ShouldCreateDoctor_WithPendingApprovalStatusAsync()
  {
    var (service, mocks) = CreateService();

    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-doctor", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = CreateDoctorRequest();

    var result = await service.RegisterAsync("new-doctor", request, CancellationToken.None);

    result.Role.Should().Be("Doctor");
    result.RegistrationStatus.Should().Be("pending_approval");

    mocks.UserRepo.Verify(x => x.AddAsync(It.Is<User>(u =>
      u.Role == UserRole.Doctor
      && u.RegistrationStatus == RegistrationStatus.PendingApproval
      && !u.IsActive), It.IsAny<CancellationToken>()), Times.Once);

    mocks.DoctorRepo.Verify(x => x.AddAsync(It.Is<DoctorProfile>(p =>
      p.MedicalLicenseNumber == "MED-12345"
      && p.Specialization == "Cardiology"), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task RegisterAsync_ShouldThrow_WhenDoctorDataMissingForDoctorRoleAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-doctor", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = new RegisterUserRequest(
      "doctor", "Test", "Doctor", "doc@aarogya.dev",
      null, null, null, null, null, null, null, null);

    var action = async () => await service.RegisterAsync("new-doctor", request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*DoctorData is required*");
  }

  #endregion

  #region RegisterAsync — Lab Technician

  [Fact]
  public async Task RegisterAsync_ShouldCreateLabTechnician_WithPendingApprovalStatusAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-lab", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = CreateLabTechnicianRequest();

    var result = await service.RegisterAsync("new-lab", request, CancellationToken.None);

    result.Role.Should().Be("LabTechnician");
    result.RegistrationStatus.Should().Be("pending_approval");

    mocks.LabTechRepo.Verify(x => x.AddAsync(It.Is<LabTechnicianProfile>(p =>
      p.LabName == "City Lab"), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task RegisterAsync_ShouldThrow_WhenLabDataMissingForLabRoleAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-lab", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = new RegisterUserRequest(
      "lab_technician", "Test", "Lab", "lab@aarogya.dev",
      null, null, null, null, null, null, null, null);

    var action = async () => await service.RegisterAsync("new-lab", request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*LabTechnicianData is required*");
  }

  #endregion

  #region RegisterAsync — Validation

  [Fact]
  public async Task RegisterAsync_ShouldThrow_WhenUserAlreadyExistsAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("existing-user", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new User { Id = Guid.NewGuid(), FirstName = "X", LastName = "Y", Email = "x@y.z" });

    var request = CreatePatientRequest();

    var action = async () => await service.RegisterAsync("existing-user", request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*already registered*");
  }

  [Fact]
  public async Task RegisterAsync_ShouldThrow_WhenRoleUnsupportedAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-user", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = new RegisterUserRequest(
      "invalid_role", "Test", "User", "test@aarogya.dev",
      null, null, null, null, null, null, null, null);

    var action = async () => await service.RegisterAsync("new-user", request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*Unsupported role*");
  }

  #endregion

  #region RegisterAsync — Consents

  [Fact]
  public async Task RegisterAsync_ShouldCreateConsentRecords_WhenConsentsProvidedAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-patient", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = CreatePatientRequest(
    [
      new InitialConsentGrant("profile_management", true),
      new InitialConsentGrant("medical_data_sharing", true)
    ]);

    var result = await service.RegisterAsync("new-patient", request, CancellationToken.None);

    result.ConsentsGranted.Should().HaveCount(2);
    result.ConsentsGranted.Should().Contain("profile_management");
    result.ConsentsGranted.Should().Contain("medical_data_sharing");

    mocks.ConsentRepo.Verify(x => x.AddAsync(
      It.IsAny<ConsentRecord>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
  }

  [Fact]
  public async Task RegisterAsync_ShouldReturnEmptyConsents_WhenNoConsentsProvidedAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-patient", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = CreatePatientRequest();
    var result = await service.RegisterAsync("new-patient", request, CancellationToken.None);

    result.ConsentsGranted.Should().BeEmpty();
  }

  [Fact]
  public async Task RegisterAsync_ShouldThrow_WhenConsentPurposeUnsupportedAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("new-patient", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var request = CreatePatientRequest([new InitialConsentGrant("unsupported_purpose", true)]);

    var action = async () => await service.RegisterAsync("new-patient", request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*Unsupported consent purpose*");
  }

  #endregion

  #region GetRegistrationStatusAsync

  [Fact]
  public async Task GetRegistrationStatusAsync_ShouldReturnRegistrationRequired_WhenUserNotFoundAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("unknown-user", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var result = await service.GetRegistrationStatusAsync("unknown-user", CancellationToken.None);

    result.Sub.Should().Be("unknown-user");
    result.RegistrationStatus.Should().Be("registration_required");
    result.Role.Should().BeEmpty();
  }

  [Fact]
  public async Task GetRegistrationStatusAsync_ShouldReturnApproved_WhenUserExistsAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("approved-user", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new User
      {
        Id = Guid.NewGuid(),
        ExternalAuthId = "approved-user",
        Role = UserRole.Patient,
        RegistrationStatus = RegistrationStatus.Approved,
        FirstName = "Test",
        LastName = "User",
        Email = "test@aarogya.dev"
      });

    var result = await service.GetRegistrationStatusAsync("approved-user", CancellationToken.None);

    result.Sub.Should().Be("approved-user");
    result.RegistrationStatus.Should().Be("approved");
    result.Role.Should().Be("Patient");
  }

  [Fact]
  public async Task GetRegistrationStatusAsync_ShouldReturnPendingApproval_ForPendingDoctorAsync()
  {
    var (service, mocks) = CreateService();
    mocks.UserRepo
      .Setup(x => x.GetByExternalAuthIdAsync("pending-doctor", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new User
      {
        Id = Guid.NewGuid(),
        ExternalAuthId = "pending-doctor",
        Role = UserRole.Doctor,
        RegistrationStatus = RegistrationStatus.PendingApproval,
        FirstName = "Test",
        LastName = "Doctor",
        Email = "doc@aarogya.dev"
      });

    var result = await service.GetRegistrationStatusAsync("pending-doctor", CancellationToken.None);

    result.RegistrationStatus.Should().Be("pending_approval");
    result.Role.Should().Be("Doctor");
  }

  #endregion

  #region Helpers

  private static RegisterUserRequest CreatePatientRequest(
    IReadOnlyList<InitialConsentGrant>? consents = null)
  {
    return new RegisterUserRequest(
      "patient", "Test", "Patient", "test@aarogya.dev",
      "+919876543210", new DateOnly(1990, 1, 1), "male", "Pune", "O+",
      null, null, consents);
  }

  private static RegisterUserRequest CreateDoctorRequest()
  {
    return new RegisterUserRequest(
      "doctor", "Test", "Doctor", "doc@aarogya.dev",
      "+919876543211", null, null, null, null,
      new DoctorRegistrationData("MED-12345", "Cardiology", "City Hospital", "Mumbai"),
      null, null);
  }

  private static RegisterUserRequest CreateLabTechnicianRequest()
  {
    return new RegisterUserRequest(
      "lab_technician", "Test", "Lab", "lab@aarogya.dev",
      "+919876543212", null, null, null, null,
      null, new LabTechnicianRegistrationData("City Lab", "LAB-001", "NABL-001"),
      null);
  }

  private static (UserRegistrationService Service, ServiceMocks Mocks) CreateService()
  {
    var mocks = new ServiceMocks();
    var service = new UserRegistrationService(
      mocks.UserRepo.Object,
      mocks.DoctorRepo.Object,
      mocks.LabTechRepo.Object,
      mocks.ConsentRepo.Object,
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
    public Mock<IConsentRecordRepository> ConsentRepo { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public Mock<IAuditLoggingService> AuditService { get; } = new();
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }

  #endregion
}
