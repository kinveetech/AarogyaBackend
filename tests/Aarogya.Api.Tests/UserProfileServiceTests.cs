using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Infrastructure.Aadhaar;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class UserProfileServiceTests
{
  [Fact]
  public async Task UpdateCurrentUserAsync_ShouldUpdateOnlyProvidedFieldsAsync()
  {
    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Before",
      LastName = "Patient",
      Email = "before@aarogya.dev",
      Phone = "+919876543210",
      Address = "Old Address",
      BloodGroup = "O+",
      DateOfBirth = new DateOnly(1990, 1, 1),
      CreatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
      UpdatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
    };

    var repository = new Mock<IUserRepository>();
    repository.Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var unitOfWork = new Mock<IUnitOfWork>();
    var aadhaarVaultService = new Mock<IAadhaarVaultService>();

    var clock = new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero));

    var service = new UserProfileService(repository.Object, unitOfWork.Object, aadhaarVaultService.Object, Mock.Of<IAuditLoggingService>(), clock);

    var response = await service.UpdateCurrentUserAsync(
      "seed-PATIENT-1",
      new UpdateUserProfileRequest(
        " After ",
        null,
        " after@aarogya.dev ",
        null,
        " New Address ",
        " ab+ ",
        null),
      CancellationToken.None);

    user.FirstName.Should().Be("After");
    user.LastName.Should().Be("Patient");
    user.Email.Should().Be("after@aarogya.dev");
    user.Address.Should().Be("New Address");
    user.BloodGroup.Should().Be("AB+");
    user.Phone.Should().Be("+919876543210");
    user.UpdatedAt.Should().Be(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero));

    response.FirstName.Should().Be("After");
    response.BloodGroup.Should().Be("AB+");

    repository.Verify(x => x.Update(user), Times.Once);
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task GetCurrentUserAsync_ShouldThrow_WhenUserNotFoundAsync()
  {
    var repository = new Mock<IUserRepository>();
    repository.Setup(x => x.GetByExternalAuthIdAsync("missing-sub", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

    var service = new UserProfileService(
      repository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAadhaarVaultService>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero)));

    var action = async () => await service.GetCurrentUserAsync("missing-sub", CancellationToken.None);

    await action.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task VerifyCurrentUserAadhaarAsync_ShouldPersistReferenceTokenAndHashAsync()
  {
    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Before",
      LastName = "Patient",
      Email = "before@aarogya.dev"
    };

    var repository = new Mock<IUserRepository>();
    repository.Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var unitOfWork = new Mock<IUnitOfWork>();
    var aadhaarVaultService = new Mock<IAadhaarVaultService>();
    var referenceToken = Guid.NewGuid();
    aadhaarVaultService
      .Setup(x => x.VerifyAndCreateReferenceTokenAsync(
        "123456789012", user.Id, "Ravi", "Kumar", new DateOnly(1990, 5, 15), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new AadhaarVerificationResult(
        referenceToken,
        false,
        "LOCAL",
        new MockAadhaarDemographics("Verified Holder", null, "F", "India"),
        "request-1"));

    var clock = new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero));
    var service = new UserProfileService(repository.Object, unitOfWork.Object, aadhaarVaultService.Object, Mock.Of<IAuditLoggingService>(), clock);

    var response = await service.VerifyCurrentUserAadhaarAsync(
      "seed-PATIENT-1",
      new VerifyAadhaarRequest("123456789012", "Ravi", "Kumar", new DateOnly(1990, 5, 15)),
      CancellationToken.None);

    response.ReferenceToken.Should().Be(referenceToken);
    response.Provider.Should().Be("LOCAL");
    user.AadhaarRefToken.Should().Be(referenceToken);
    user.AadhaarSha256.Should().NotBeNull();
    user.UpdatedAt.Should().Be(clock.UtcNow);

    repository.Verify(x => x.Update(user), Times.Once);
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task VerifyCurrentUserAadhaarAsync_ShouldRejectNonPatientAsync()
  {
    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-DOCTOR-1",
      Role = UserRole.Doctor,
      FirstName = "Doctor",
      LastName = "One",
      Email = "doctor@aarogya.dev"
    };

    var repository = new Mock<IUserRepository>();
    repository.Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var service = new UserProfileService(
      repository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAadhaarVaultService>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero)));

    var action = async () => await service.VerifyCurrentUserAadhaarAsync(
      "seed-DOCTOR-1",
      new VerifyAadhaarRequest("123456789012", "Doctor", "One", new DateOnly(1985, 3, 10)),
      CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*patient profiles*");
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
