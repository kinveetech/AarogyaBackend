using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class AccessGrantServiceTests
{
  [Fact]
  public async Task CreateAsync_ShouldCreateAllReportsGrant_WithDefaultExpiryAsync()
  {
    var now = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
    var patient = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-PATIENT-1", Role = UserRole.Patient };
    var doctor = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-DOCTOR-1", Role = UserRole.Doctor };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(doctor);

    AccessGrant? created = null;
    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    accessGrantRepository
      .Setup(x => x.GetActiveGrantAsync(patient.Id, doctor.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync((AccessGrant?)null);
    accessGrantRepository
      .Setup(x => x.AddAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>()))
      .Callback<AccessGrant, CancellationToken>((grant, _) => created = grant)
      .Returns(Task.CompletedTask);

    var service = new AccessGrantService(
      userRepository.Object,
      Mock.Of<IReportRepository>(),
      accessGrantRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Options.Create(new AccessGrantOptions
      {
        DefaultExpiryDays = 30,
        MaxExpiryDays = 180
      }),
      new FixedUtcClock(now));

    var result = await service.CreateAsync(
      "seed-PATIENT-1",
      new CreateAccessGrantRequest("seed-DOCTOR-1", true, null, "care-coordination", null),
      CancellationToken.None);

    result.AllReports.Should().BeTrue();
    result.ReportIds.Should().BeEmpty();
    result.Purpose.Should().Be("care-coordination");
    result.ExpiresAt.Should().Be(now.AddDays(30));
    created.Should().NotBeNull();
    created!.Scope.AllowedReportIds.Should().BeEmpty();
  }

  [Fact]
  public async Task CreateAsync_ShouldRejectReportIdsOutsidePatientScopeAsync()
  {
    var patient = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-PATIENT-1", Role = UserRole.Patient };
    var doctor = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-DOCTOR-1", Role = UserRole.Doctor };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(doctor);

    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.ListByPatientAsync(patient.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(
      [
        new Report { Id = Guid.NewGuid(), PatientId = patient.Id }
      ]);

    var service = new AccessGrantService(
      userRepository.Object,
      reportRepository.Object,
      Mock.Of<IAccessGrantRepository>(),
      Mock.Of<IUnitOfWork>(),
      Options.Create(new AccessGrantOptions()),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.CreateAsync(
      "seed-PATIENT-1",
      new CreateAccessGrantRequest("seed-DOCTOR-1", false, [Guid.NewGuid()], "follow-up", DateTimeOffset.UtcNow.AddDays(7)),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*belong to the patient*");
  }

  [Fact]
  public async Task RevokeAsync_ShouldMarkGrantAsRevokedAsync()
  {
    var patient = new User { Id = Guid.NewGuid(), ExternalAuthId = "seed-PATIENT-1", Role = UserRole.Patient };
    var grant = new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = patient.Id,
      Status = AccessGrantStatus.Active
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    accessGrantRepository
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(grant);

    var unitOfWork = new Mock<IUnitOfWork>();
    var service = new AccessGrantService(
      userRepository.Object,
      Mock.Of<IReportRepository>(),
      accessGrantRepository.Object,
      unitOfWork.Object,
      Options.Create(new AccessGrantOptions()),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var revoked = await service.RevokeAsync("seed-PATIENT-1", grant.Id, CancellationToken.None);

    revoked.Should().BeTrue();
    grant.Status.Should().Be(AccessGrantStatus.Revoked);
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
