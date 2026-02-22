using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class CachedAccessGrantServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task GetForPatientAsync_ShouldReturnCachedResponse_WhenCacheHitAsync()
  {
    var cached = new[]
    {
      new AccessGrantResponse(
        Guid.NewGuid(), "seed-PATIENT-1", "seed-DOCTOR-1", true, [], "treatment",
        FixedNow, FixedNow.AddDays(30), false)
    };

    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetNamespaceVersionAsync(EntityCacheNamespaces.AccessGrantListings, It.IsAny<CancellationToken>()))
      .ReturnsAsync("1");
    cacheService
      .Setup(x => x.GetAsync<AccessGrantResponse[]>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(cached);

    var (sut, _) = CreateService(cacheService: cacheService.Object);

    var result = await sut.GetForPatientAsync("seed-PATIENT-1", CancellationToken.None);

    result.Should().BeSameAs(cached);
    cacheService.Verify(
      x => x.SetAsync(
        It.IsAny<string>(),
        It.IsAny<AccessGrantResponse[]>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetForPatientAsync_ShouldCallInnerAndPopulateCache_WhenCacheMissAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);

    var accessGrantRepo = new Mock<IAccessGrantRepository>();
    accessGrantRepo
      .Setup(x => x.ListAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetNamespaceVersionAsync(EntityCacheNamespaces.AccessGrantListings, It.IsAny<CancellationToken>()))
      .ReturnsAsync("1");
    cacheService
      .Setup(x => x.GetAsync<AccessGrantResponse[]>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((AccessGrantResponse[]?)null);

    var (sut, userRepo) = CreateService(
      accessGrantRepo: accessGrantRepo.Object,
      cacheService: cacheService.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var result = await sut.GetForPatientAsync("seed-PATIENT-1", CancellationToken.None);

    result.Should().BeEmpty();
    cacheService.Verify(
      x => x.SetAsync(
        It.IsAny<string>(),
        It.IsAny<AccessGrantResponse[]>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetForDoctorAsync_ShouldReturnCachedResponse_WhenCacheHitAsync()
  {
    var cached = new[]
    {
      new AccessGrantResponse(
        Guid.NewGuid(), "seed-PATIENT-1", "seed-DOCTOR-1", true, [], "treatment",
        FixedNow, FixedNow.AddDays(30), false)
    };

    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetNamespaceVersionAsync(EntityCacheNamespaces.AccessGrantListings, It.IsAny<CancellationToken>()))
      .ReturnsAsync("1");
    cacheService
      .Setup(x => x.GetAsync<AccessGrantResponse[]>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(cached);

    var (sut, _) = CreateService(cacheService: cacheService.Object);

    var result = await sut.GetForDoctorAsync("seed-DOCTOR-1", CancellationToken.None);

    result.Should().BeSameAs(cached);
  }

  [Fact]
  public async Task CreateAsync_ShouldBumpBothNamespaceVersionsAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);
    var doctor = CreateUser("seed-DOCTOR-1", UserRole.Doctor);

    var accessGrantRepo = new Mock<IAccessGrantRepository>();
    accessGrantRepo
      .Setup(x => x.AddAsync(It.IsAny<AccessGrant>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var bumpedNamespaces = new List<string>();
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.BumpNamespaceVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((ns, _) => bumpedNamespaces.Add(ns))
      .Returns(Task.CompletedTask);

    var (sut, userRepo) = CreateService(
      accessGrantRepo: accessGrantRepo.Object,
      cacheService: cacheService.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(doctor);

    var request = new CreateAccessGrantRequest("seed-DOCTOR-1", true, null, "treatment");
    await sut.CreateAsync("seed-PATIENT-1", request, CancellationToken.None);

    bumpedNamespaces.Should().Contain(EntityCacheNamespaces.AccessGrantListings);
    bumpedNamespaces.Should().Contain(EntityCacheNamespaces.ReportListings);
  }

  [Fact]
  public async Task RevokeAsync_ShouldBumpBothNamespaceVersions_WhenRevokedAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);
    var grantId = Guid.NewGuid();
    var grant = new AccessGrant
    {
      Id = grantId,
      PatientId = patient.Id,
      GrantedToUserId = Guid.NewGuid(),
      GrantedByUserId = patient.Id,
      GrantReason = "treatment",
      Status = AccessGrantStatus.Active,
      StartsAt = FixedNow,
      ExpiresAt = FixedNow.AddDays(30)
    };

    var accessGrantRepo = new Mock<IAccessGrantRepository>();
    accessGrantRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(grant);

    var bumpedNamespaces = new List<string>();
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.BumpNamespaceVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((ns, _) => bumpedNamespaces.Add(ns))
      .Returns(Task.CompletedTask);

    var (sut, userRepo) = CreateService(
      accessGrantRepo: accessGrantRepo.Object,
      cacheService: cacheService.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    await sut.RevokeAsync("seed-PATIENT-1", grantId, CancellationToken.None);

    bumpedNamespaces.Should().Contain(EntityCacheNamespaces.AccessGrantListings);
    bumpedNamespaces.Should().Contain(EntityCacheNamespaces.ReportListings);
  }

  [Fact]
  public async Task RevokeAsync_ShouldNotBumpNamespaceVersions_WhenGrantNotFoundAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);

    var accessGrantRepo = new Mock<IAccessGrantRepository>();
    accessGrantRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((AccessGrant?)null);

    var cacheService = new Mock<IEntityCacheService>();

    var (sut, userRepo) = CreateService(
      accessGrantRepo: accessGrantRepo.Object,
      cacheService: cacheService.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    await sut.RevokeAsync("seed-PATIENT-1", Guid.NewGuid(), CancellationToken.None);

    cacheService.Verify(
      x => x.BumpNamespaceVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  private static (CachedAccessGrantService Sut, Mock<IUserRepository> UserRepo) CreateService(
    IAccessGrantRepository? accessGrantRepo = null,
    IEntityCacheService? cacheService = null,
    EntityCacheOptions? cacheOptions = null)
  {
    var userRepo = new Mock<IUserRepository>();

    var innerService = new AccessGrantService(
      userRepo.Object,
      Mock.Of<IReportRepository>(),
      accessGrantRepo ?? Mock.Of<IAccessGrantRepository>(),
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAuditLoggingService>(),
      Mock.Of<ITransactionalEmailNotificationService>(),
      Options.Create(new AccessGrantOptions()),
      new FixedUtcClock(FixedNow));

    var sut = new CachedAccessGrantService(
      innerService,
      cacheService ?? Mock.Of<IEntityCacheService>(),
      Options.Create(cacheOptions ?? new EntityCacheOptions()));

    return (sut, userRepo);
  }

  private static User CreateUser(string sub, UserRole role) =>
    new()
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = sub,
      Role = role,
      FirstName = "Test",
      LastName = "User",
      Email = "test@example.com"
    };

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
