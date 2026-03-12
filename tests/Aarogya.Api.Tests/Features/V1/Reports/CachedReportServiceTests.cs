using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class CachedReportServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);

  private static readonly ReportListResponse SampleListResponse = new(
    1, 20, 1,
    [new ReportSummaryResponse(Guid.NewGuid(), "Blood Report", "blood_test", "processing", FixedNow, null, null)]);

  [Fact]
  public async Task GetForUserAsync_ShouldReturnCachedResponse_WhenCacheHitAsync()
  {
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, It.IsAny<CancellationToken>()))
      .ReturnsAsync("1");
    cacheService
      .Setup(x => x.GetAsync<ReportListResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(SampleListResponse);

    var (sut, _) = CreateService(cacheService: cacheService.Object);

    var request = new ReportListQueryRequest();
    var result = await sut.GetForUserAsync("seed-PATIENT-1", request, CancellationToken.None);

    result.Should().BeSameAs(SampleListResponse);
    cacheService.Verify(
      x => x.SetAsync(
        It.IsAny<string>(),
        It.IsAny<ReportListResponse>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetForUserAsync_ShouldCallInnerAndPopulateCache_WhenCacheMissAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.ListByPatientAsync(patient.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<Report>());

    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, It.IsAny<CancellationToken>()))
      .ReturnsAsync("1");
    cacheService
      .Setup(x => x.GetAsync<ReportListResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((ReportListResponse?)null);

    var (sut, userRepo) = CreateService(
      reportRepo: reportRepo.Object,
      cacheService: cacheService.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var request = new ReportListQueryRequest();
    var result = await sut.GetForUserAsync("seed-PATIENT-1", request, CancellationToken.None);

    result.Should().NotBeNull();
    cacheService.Verify(
      x => x.SetAsync(
        It.IsAny<string>(),
        It.IsAny<ReportListResponse>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task AddForUserAsync_ShouldBumpReportListingsNamespaceVersionAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    reportRepo
      .Setup(x => x.GetByReportNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    var s3Mock = new Mock<IAmazonS3>();
    s3Mock
      .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new GetObjectMetadataResponse
      {
        Headers = { ContentType = "application/pdf", ContentLength = 1024 },
        ChecksumSHA256 = "abc123"
      });

    var bumpedNamespaces = new List<string>();
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.BumpNamespaceVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((ns, _) => bumpedNamespaces.Add(ns))
      .Returns(Task.CompletedTask);

    var (sut, userRepo) = CreateService(
      reportRepo: reportRepo.Object,
      cacheService: cacheService.Object,
      s3Client: s3Mock.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var request = new CreateReportRequest(
      "other", "reports/seed-PATIENT-1/2026/02/report.pdf", null, null, null, null, null, null, []);
    await sut.AddForUserAsync("seed-PATIENT-1", request, CancellationToken.None);

    bumpedNamespaces.Should().Contain(EntityCacheNamespaces.ReportListings);
  }

  [Fact]
  public async Task SoftDeleteForUserAsync_ShouldBumpNamespaceVersion_WhenDeletedAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);
    var reportId = Guid.NewGuid();
    var report = new Report
    {
      Id = reportId,
      ReportNumber = "RPT-TEST123456",
      PatientId = patient.Id,
      UploadedByUserId = patient.Id,
      ReportType = ReportType.Other,
      Status = ReportStatus.Processing,
      UploadedAt = FixedNow,
      FileStorageKey = "reports/test.pdf"
    };

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var bumpedNamespaces = new List<string>();
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.BumpNamespaceVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((ns, _) => bumpedNamespaces.Add(ns))
      .Returns(Task.CompletedTask);

    var (sut, userRepo) = CreateService(
      reportRepo: reportRepo.Object,
      cacheService: cacheService.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    await sut.SoftDeleteForUserAsync("seed-PATIENT-1", reportId, CancellationToken.None);

    bumpedNamespaces.Should().Contain(EntityCacheNamespaces.ReportListings);
  }

  [Fact]
  public async Task SoftDeleteForUserAsync_ShouldNotBumpNamespaceVersion_WhenNotFoundAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    var cacheService = new Mock<IEntityCacheService>();

    var (sut, userRepo) = CreateService(
      reportRepo: reportRepo.Object,
      cacheService: cacheService.Object);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    await sut.SoftDeleteForUserAsync("seed-PATIENT-1", Guid.NewGuid(), CancellationToken.None);

    cacheService.Verify(
      x => x.BumpNamespaceVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetForUserAsync_ShouldUseTtlFromOptions_WhenPopulatingCacheAsync()
  {
    var patient = CreateUser("seed-PATIENT-1", UserRole.Patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.ListByPatientAsync(patient.Id, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Array.Empty<Report>());

    TimeSpan? capturedTtl = null;
    var cacheService = new Mock<IEntityCacheService>();
    cacheService
      .Setup(x => x.GetNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, It.IsAny<CancellationToken>()))
      .ReturnsAsync("1");
    cacheService
      .Setup(x => x.GetAsync<ReportListResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((ReportListResponse?)null);
    cacheService
      .Setup(x => x.SetAsync(
        It.IsAny<string>(),
        It.IsAny<ReportListResponse>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()))
      .Callback<string, ReportListResponse, TimeSpan, CancellationToken>((_, _, ttl, _) => capturedTtl = ttl)
      .Returns(Task.CompletedTask);

    var options = new EntityCacheOptions { ReportListingTtlSeconds = 600 };
    var (sut, userRepo) = CreateService(
      reportRepo: reportRepo.Object,
      cacheService: cacheService.Object,
      cacheOptions: options);
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    await sut.GetForUserAsync("seed-PATIENT-1", new ReportListQueryRequest(), CancellationToken.None);

    capturedTtl.Should().Be(TimeSpan.FromSeconds(600));
  }

  private static (CachedReportService Sut, Mock<IUserRepository> UserRepo) CreateService(
    IReportRepository? reportRepo = null,
    IEntityCacheService? cacheService = null,
    EntityCacheOptions? cacheOptions = null,
    IAmazonS3? s3Client = null)
  {
    var userRepo = new Mock<IUserRepository>();

    var innerService = new ReportService(
      s3Client ?? Mock.Of<IAmazonS3>(),
      Mock.Of<ICloudFrontInvalidationService>(),
      userRepo.Object,
      Mock.Of<IAccessGrantRepository>(),
      reportRepo ?? Mock.Of<IReportRepository>(),
      Mock.Of<IAuditLoggingService>(),
      Mock.Of<IPatientNotificationService>(),
      Mock.Of<IUnitOfWork>(),
      Options.Create(new AwsOptions { S3 = new S3Options { BucketName = "test-bucket" } }),
      new FixedUtcClock(FixedNow));

    var sut = new CachedReportService(
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
