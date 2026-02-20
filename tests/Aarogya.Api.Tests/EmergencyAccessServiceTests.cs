using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.EmergencyAccess;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class EmergencyAccessServiceTests
{
  [Fact]
  public async Task RequestAsync_ShouldCreateGrantAndNotify_WhenContactRegisteredAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Pat",
      LastName = "One",
      Phone = "+919876543210",
      Email = "pat@example.com"
    };
    var doctor = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-DOCTOR-1",
      Role = UserRole.Doctor,
      FirstName = "Doc",
      LastName = "One"
    };
    var contact = new EmergencyContact
    {
      Id = Guid.NewGuid(),
      UserId = patient.Id,
      Name = "Kin One",
      Phone = "+919876543210"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository.Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(patient);
    userRepository.Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>())).ReturnsAsync(doctor);

    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    emergencyContactRepository.Setup(x => x.ListByUserAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync([contact]);

    AccessGrant? createdGrant = null;
    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    accessGrantRepository.Setup(x => x.GetActiveGrantAsync(patient.Id, doctor.Id, It.IsAny<CancellationToken>())).ReturnsAsync((AccessGrant?)null);
    accessGrantRepository
      .Setup(x => x.AddAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>()))
      .Callback<AccessGrant, CancellationToken>((grant, _) => createdGrant = grant)
      .Returns(Task.CompletedTask);

    var email = new Mock<ITransactionalEmailNotificationService>();
    var sms = new Mock<ICriticalSmsNotificationService>();
    var push = new Mock<IPushNotificationService>();

    var service = new EmergencyAccessService(
      userRepository.Object,
      emergencyContactRepository.Object,
      accessGrantRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      email.Object,
      sms.Object,
      push.Object,
      Options.Create(new EmergencyAccessOptions
      {
        DefaultDurationHours = 24,
        MinDurationHours = 24,
        MaxDurationHours = 48
      }),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)));

    var response = await service.RequestAsync(
      new CreateEmergencyAccessRequest("seed-PATIENT-1", "+919876543210", "seed-DOCTOR-1", "accident", 24),
      CancellationToken.None);

    response.PatientSub.Should().Be("seed-PATIENT-1");
    response.DoctorSub.Should().Be("seed-DOCTOR-1");
    response.EmergencyContactId.Should().Be(contact.Id);
    createdGrant.Should().NotBeNull();
    createdGrant!.GrantReason.Should().StartWith("emergency:");

    email.Verify(
      x => x.SendEmergencyAccessRequestedAsync(
        patient,
        contact,
        doctor,
        It.IsAny<AccessGrant>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
    sms.Verify(
      x => x.SendEmergencyAccessRequestedAsync(
        patient,
        contact,
        doctor,
        It.IsAny<AccessGrant>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
    push.Verify(
      x => x.SendToUserAsync(
        "seed-PATIENT-1",
        NotificationEventTypes.EmergencyAccess,
        It.IsAny<SendPushNotificationRequest>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task RequestAsync_ShouldReject_WhenContactNotRegisteredAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient
    };
    var doctor = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-DOCTOR-1",
      Role = UserRole.Doctor
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository.Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(patient);
    userRepository.Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>())).ReturnsAsync(doctor);

    var emergencyContactRepository = new Mock<IEmergencyContactRepository>();
    emergencyContactRepository.Setup(x => x.ListByUserAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

    var service = new EmergencyAccessService(
      userRepository.Object,
      emergencyContactRepository.Object,
      Mock.Of<IAccessGrantRepository>(),
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      Mock.Of<ITransactionalEmailNotificationService>(),
      Mock.Of<ICriticalSmsNotificationService>(),
      Mock.Of<IPushNotificationService>(),
      Options.Create(new EmergencyAccessOptions()),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)));

    var action = async () => await service.RequestAsync(
      new CreateEmergencyAccessRequest("seed-PATIENT-1", "+919876543210", "seed-DOCTOR-1", "accident", 24),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*Only registered emergency contacts can request*");
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
