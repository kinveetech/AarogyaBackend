using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class EmergencyContactServiceTests
{
  [Fact]
  public async Task AddForUserAsync_ShouldCreateContact_ForPatientAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    emergencyContactRepository
      .Setup(x => x.ListByUserAsync(patient.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    EmergencyContact? created = null;
    emergencyContactRepository
      .Setup(x => x.AddAsync(It.IsAny<EmergencyContact>(), It.IsAny<CancellationToken>()))
      .Callback<EmergencyContact, CancellationToken>((contact, _) => created = contact)
      .Returns(Task.CompletedTask);

    var service = new EmergencyContactService(
      userRepository.Object,
      emergencyContactRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)));

    var response = await service.AddForUserAsync(
      "seed-PATIENT-1",
      new CreateEmergencyContactRequest("Kin One", "+919876543210", "brother", "kin.one@example.com"),
      CancellationToken.None);

    response.Name.Should().Be("Kin One");
    response.Email.Should().Be("kin.one@example.com");
    created.Should().NotBeNull();
    created!.UserId.Should().Be(patient.Id);
  }

  [Fact]
  public async Task AddForUserAsync_ShouldReject_WhenMaxContactsReachedAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    emergencyContactRepository
      .Setup(x => x.ListByUserAsync(patient.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(
      [
        new EmergencyContact(),
        new EmergencyContact(),
        new EmergencyContact()
      ]);

    var service = new EmergencyContactService(
      userRepository.Object,
      emergencyContactRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.AddForUserAsync(
      "seed-PATIENT-1",
      new CreateEmergencyContactRequest("Kin One", "+919876543210", "brother"),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Maximum of 3 emergency contacts*");
  }

  [Fact]
  public async Task UpdateForUserAsync_ShouldUpdateExistingContactAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient
    };

    var contact = new EmergencyContact
    {
      Id = Guid.NewGuid(),
      UserId = patient.Id,
      Name = "Old",
      Phone = "+919876543210",
      Relationship = "brother"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    emergencyContactRepository
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<EmergencyContact>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(contact);

    var service = new EmergencyContactService(
      userRepository.Object,
      emergencyContactRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var updated = await service.UpdateForUserAsync(
      "seed-PATIENT-1",
      contact.Id,
      new UpdateEmergencyContactRequest("New", "+919812345678", "father", "new@example.com"),
      CancellationToken.None);

    updated.Should().NotBeNull();
    updated!.Name.Should().Be("New");
    updated.Email.Should().Be("new@example.com");
  }

  [Fact]
  public async Task DeleteForUserAsync_ShouldReturnFalse_WhenContactNotFoundAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    emergencyContactRepository
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<EmergencyContact>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((EmergencyContact?)null);

    var service = new EmergencyContactService(
      userRepository.Object,
      emergencyContactRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var deleted = await service.DeleteForUserAsync("seed-PATIENT-1", Guid.NewGuid(), CancellationToken.None);

    deleted.Should().BeFalse();
  }

  [Fact]
  public async Task AddForUserAsync_ShouldRejectNonPatientUserAsync()
  {
    var doctor = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-DOCTOR-1",
      Role = UserRole.Doctor
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(doctor);

    var service = new EmergencyContactService(
      userRepository.Object,
      Mock.Of<IEmergencyContactRepository>(),
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.AddForUserAsync(
      "seed-DOCTOR-1",
      new CreateEmergencyContactRequest("Kin One", "+919876543210", "brother"),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*Only patient users can manage emergency contacts*");
  }

  [Fact]
  public async Task DeleteForUserAsync_ShouldDeleteContact_WhenFoundAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient
    };

    var contact = new EmergencyContact
    {
      Id = Guid.NewGuid(),
      UserId = patient.Id,
      Name = "Kin One",
      Phone = "+919876543210",
      Relationship = "brother"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    emergencyContactRepository
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<EmergencyContact>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(contact);

    var unitOfWork = new Mock<IUnitOfWork>();
    var service = new EmergencyContactService(
      userRepository.Object,
      emergencyContactRepository.Object,
      unitOfWork.Object,
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var deleted = await service.DeleteForUserAsync("seed-PATIENT-1", contact.Id, CancellationToken.None);

    deleted.Should().BeTrue();
    emergencyContactRepository.Verify(x => x.Delete(contact), Times.Once);
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
