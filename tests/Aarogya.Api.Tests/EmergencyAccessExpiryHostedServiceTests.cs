using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.EmergencyAccess;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class EmergencyAccessExpiryHostedServiceTests
{
  [Fact]
  public async Task RunCycleAsync_ShouldSendPreExpiryNotificationAndExpireOverdueGrantsAsync()
  {
    var now = new DateTimeOffset(2026, 02, 20, 10, 0, 0, TimeSpan.Zero);
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

    var preExpiryGrant = new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = patient.Id,
      GrantedToUserId = doctor.Id,
      Status = AccessGrantStatus.Active,
      StartsAt = now.AddHours(-1),
      ExpiresAt = now.AddMinutes(30),
      GrantReason = "emergency:accident",
      Patient = patient,
      GrantedToUser = doctor
    };
    var expiredGrant = new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = patient.Id,
      GrantedToUserId = doctor.Id,
      Status = AccessGrantStatus.Active,
      StartsAt = now.AddHours(-5),
      ExpiresAt = now.AddMinutes(-10),
      GrantReason = "emergency:fainting",
      Patient = patient,
      GrantedToUser = doctor
    };

    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    accessGrantRepository
      .Setup(x => x.ListAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((ISpecification<AccessGrant> spec, CancellationToken _) =>
      {
        if (spec is EmergencyAccessGrantsDueForPreExpiryNotificationSpecification)
        {
          return [preExpiryGrant];
        }

        if (spec is EmergencyAccessGrantsExpiredSpecification)
        {
          return [expiredGrant];
        }

        return [];
      });

    var unitOfWork = new Mock<IUnitOfWork>();
    var email = new Mock<ITransactionalEmailNotificationService>();
    var sms = new Mock<ICriticalSmsNotificationService>();
    var push = new Mock<IPushNotificationService>();
    var audit = new Mock<IAuditLoggingService>();

    var service = new EmergencyAccessExpiryHostedService(
      accessGrantRepository.Object,
      unitOfWork.Object,
      audit.Object,
      email.Object,
      sms.Object,
      push.Object,
      Options.Create(new EmergencyAccessOptions
      {
        EnableAutoExpiryWorker = true,
        AutoExpiryWorkerIntervalMinutes = 5,
        PreExpiryNotificationLeadMinutes = 60
      }),
      new FixedUtcClock(now),
      NullLogger<EmergencyAccessExpiryHostedService>.Instance);

    await service.RunCycleAsync(CancellationToken.None);

    preExpiryGrant.GrantReason.Should().Contain("preexpiry_notified");
    expiredGrant.Status.Should().Be(AccessGrantStatus.Expired);
    expiredGrant.RevokedAt.Should().Be(now);

    email.Verify(x => x.SendEmergencyAccessExpiringSoonAsync(patient, doctor, preExpiryGrant, It.IsAny<CancellationToken>()), Times.Once);
    sms.Verify(x => x.SendEmergencyAccessExpiringSoonAsync(patient, doctor, preExpiryGrant, It.IsAny<CancellationToken>()), Times.Once);
    push.Verify(
      x => x.SendToUserAsync(
        patient.ExternalAuthId!,
        NotificationEventTypes.EmergencyAccess,
        It.IsAny<SendPushNotificationRequest>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
