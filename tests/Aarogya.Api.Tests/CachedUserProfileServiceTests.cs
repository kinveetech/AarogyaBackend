using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Users;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Infrastructure.Aadhaar;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class CachedUserProfileServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);

  private static readonly UserProfileResponse SampleProfile = new(
    "seed-PATIENT-1",
    "patient@example.com",
    "Jane",
    "Doe",
    "+919876543210",
    null,
    "A+",
    new DateOnly(1990, 5, 15),
    null,
    "approved",
    ["Patient"]);

  [Fact]
  public async Task GetCurrentUserAsync_ShouldReturnCachedResponse_WhenCacheHitAsync()
  {
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetAsync<UserProfileResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(SampleProfile);

    var sut = CreateCachedService(cacheService: cacheService.Object);

    var result = await sut.GetCurrentUserAsync("seed-PATIENT-1", CancellationToken.None);

    result.Should().BeSameAs(SampleProfile);
    cacheService.Verify(
      x => x.SetAsync(It.IsAny<string>(), It.IsAny<UserProfileResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetCurrentUserAsync_ShouldCallInnerAndPopulateCache_WhenCacheMissAsync()
  {
    var patient = CreatePatientUser("seed-PATIENT-1");

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetAsync<UserProfileResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((UserProfileResponse?)null);

    var sut = CreateCachedService(userRepository: userRepository.Object, cacheService: cacheService.Object);

    var result = await sut.GetCurrentUserAsync("seed-PATIENT-1", CancellationToken.None);

    result.Sub.Should().Be("seed-PATIENT-1");
    cacheService.Verify(
      x => x.SetAsync(It.IsAny<string>(), It.IsAny<UserProfileResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task UpdateCurrentUserAsync_ShouldInvalidateCache_AfterInnerCallAsync()
  {
    var patient = CreatePatientUser("seed-PATIENT-1");

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var cacheService = new Mock<IEntityCacheService>();
    var sut = CreateCachedService(userRepository: userRepository.Object, cacheService: cacheService.Object);

    var request = new UpdateUserProfileRequest("Updated", null, null, null, null, null, null);
    var result = await sut.UpdateCurrentUserAsync("seed-PATIENT-1", request, CancellationToken.None);

    result.Sub.Should().Be("seed-PATIENT-1");
    cacheService.Verify(
      x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task VerifyCurrentUserAadhaarAsync_ShouldInvalidateCache_AfterInnerCallAsync()
  {
    var patient = CreatePatientUser("seed-PATIENT-1");

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var aadhaarService = new Mock<IAadhaarVaultService>();
    aadhaarService
      .Setup(x => x.VerifyAndCreateReferenceTokenAsync(
        It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new AadhaarVerificationResult(Guid.NewGuid(), false, "test-provider", null, null));

    var cacheService = new Mock<IEntityCacheService>();
    var sut = CreateCachedService(
      userRepository: userRepository.Object,
      aadhaarVaultService: aadhaarService.Object,
      cacheService: cacheService.Object);

    var request = new VerifyAadhaarRequest("123456789012", "Jane", "Doe", new DateOnly(1990, 5, 15));
    await sut.VerifyCurrentUserAadhaarAsync("seed-PATIENT-1", request, CancellationToken.None);

    cacheService.Verify(
      x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetCurrentUserAsync_ShouldUseCacheKeyDerivedFromSubAsync()
  {
    string? capturedKey = null;
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetAsync<UserProfileResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((key, _) => capturedKey = key)
      .ReturnsAsync(SampleProfile);

    var sut = CreateCachedService(cacheService: cacheService.Object);

    await sut.GetCurrentUserAsync("seed-PATIENT-1", CancellationToken.None);

    capturedKey.Should().NotBeNull();
    capturedKey.Should().StartWith("cache:user-profile:");
  }

  [Fact]
  public async Task GetCurrentUserAsync_ShouldUseTtlFromOptions_WhenPopulatingCacheAsync()
  {
    var patient = CreatePatientUser("seed-PATIENT-1");

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    TimeSpan? capturedTtl = null;
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetAsync<UserProfileResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((UserProfileResponse?)null);
    cacheService
      .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<UserProfileResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
      .Callback<string, UserProfileResponse, TimeSpan, CancellationToken>((_, _, ttl, _) => capturedTtl = ttl)
      .Returns(Task.CompletedTask);

    var options = new EntityCacheOptions { UserProfileTtlSeconds = 600 };
    var sut = CreateCachedService(
      userRepository: userRepository.Object,
      cacheService: cacheService.Object,
      cacheOptions: options);

    await sut.GetCurrentUserAsync("seed-PATIENT-1", CancellationToken.None);

    capturedTtl.Should().Be(TimeSpan.FromSeconds(600));
  }

  private static CachedUserProfileService CreateCachedService(
    IUserRepository? userRepository = null,
    IAadhaarVaultService? aadhaarVaultService = null,
    IEntityCacheService? cacheService = null,
    EntityCacheOptions? cacheOptions = null)
  {
    var innerService = new UserProfileService(
      userRepository ?? Mock.Of<IUserRepository>(),
      Mock.Of<IUnitOfWork>(),
      aadhaarVaultService ?? Mock.Of<IAadhaarVaultService>(),
      Mock.Of<IAuditLoggingService>(),
      new FixedUtcClock(FixedNow));

    return new CachedUserProfileService(
      innerService,
      cacheService ?? Mock.Of<IEntityCacheService>(),
      Options.Create(cacheOptions ?? new EntityCacheOptions()));
  }

  private static User CreatePatientUser(string sub) =>
    new()
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = sub,
      Email = "patient@example.com",
      FirstName = "Jane",
      LastName = "Doe",
      Phone = "+919876543210",
      BloodGroup = "A+",
      DateOfBirth = new DateOnly(1990, 5, 15),
      Role = UserRole.Patient
    };

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
