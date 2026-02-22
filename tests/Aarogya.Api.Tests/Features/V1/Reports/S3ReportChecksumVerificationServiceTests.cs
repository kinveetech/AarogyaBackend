using System.Security.Cryptography;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class S3ReportChecksumVerificationServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 21, 12, 0, 0, TimeSpan.Zero);
  private const string TestUserSub = "test-user-sub-001";
  private const string TestBucketName = "aarogya-test-bucket";
  private const string TestStorageKey = "reports/test-user-sub-001/2026/02/21/abc123.pdf";

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldThrow_WhenReportIdIsEmpty()
  {
    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(Guid.NewGuid(), TestUserSub, UserRole.Patient));

    var (sut, _) = CreateService(userRepository: userRepo);
    var request = new CreateVerifiedReportDownloadRequest(Guid.Empty);

    var act = () => sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*ReportId*required*");
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldThrow_WhenUserIsNotProvisioned()
  {
    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var (sut, _) = CreateService(userRepository: userRepo);
    var request = new CreateVerifiedReportDownloadRequest(Guid.NewGuid());

    var act = () => sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*not provisioned*");
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldThrowKeyNotFound_WhenReportDoesNotExist()
  {
    var userId = Guid.NewGuid();
    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(userId, TestUserSub, UserRole.Patient));

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    var (sut, _) = CreateService(userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(Guid.NewGuid());

    var act = () => sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    await act.Should().ThrowAsync<KeyNotFoundException>()
      .WithMessage("Report not found.");
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldThrowUnauthorized_WhenUserCannotAccessReport()
  {
    var requesterId = Guid.NewGuid();
    var ownerId = Guid.NewGuid();
    var reportId = Guid.NewGuid();

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(requesterId, TestUserSub, UserRole.Patient));

    var report = CreateReport(reportId, patientId: ownerId, uploadedByUserId: ownerId);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var (sut, _) = CreateService(userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId);

    var act = () => sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    await act.Should().ThrowAsync<UnauthorizedAccessException>()
      .WithMessage("*not authorized*");
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldAllowAccess_WhenUserIsPatient()
  {
    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var fileContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
    var checksum = ComputeSha256Hex(fileContent);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(userId, TestUserSub, UserRole.Patient));

    var report = CreateReport(reportId, patientId: userId, uploadedByUserId: userId, checksum: checksum);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var s3Client = SetupS3GetObject(fileContent);
    SetupPresignedUrl(s3Client, "https://s3.example.com/signed-url");

    var (sut, _) = CreateService(s3Client: s3Client, userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId);

    var result = await sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    result.Should().NotBeNull();
    result.ReportId.Should().Be(reportId);
    result.ChecksumVerified.Should().BeTrue();
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldAllowAccess_WhenUserIsAdmin()
  {
    var adminId = Guid.NewGuid();
    var ownerId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var fileContent = new byte[] { 0x01, 0x02, 0x03 };
    var checksum = ComputeSha256Hex(fileContent);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(adminId, TestUserSub, UserRole.Admin));

    var report = CreateReport(reportId, patientId: ownerId, uploadedByUserId: ownerId, checksum: checksum);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var s3Client = SetupS3GetObject(fileContent);
    SetupPresignedUrl(s3Client, "https://s3.example.com/signed-url");

    var (sut, _) = CreateService(s3Client: s3Client, userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId);

    var result = await sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    result.ChecksumVerified.Should().BeTrue();
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldAllowAccess_WhenUserIsDoctor()
  {
    var doctorId = Guid.NewGuid();
    var ownerId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var fileContent = new byte[] { 0x01, 0x02, 0x03 };
    var checksum = ComputeSha256Hex(fileContent);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(doctorId, TestUserSub, UserRole.Doctor));

    var report = CreateReport(reportId, patientId: ownerId, uploadedByUserId: ownerId, checksum: checksum);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var s3Client = SetupS3GetObject(fileContent);
    SetupPresignedUrl(s3Client, "https://s3.example.com/signed-url");

    var (sut, _) = CreateService(s3Client: s3Client, userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId);

    var result = await sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    result.ChecksumVerified.Should().BeTrue();
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldThrow_WhenChecksumMismatch()
  {
    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var storedContent = new byte[] { 0x01, 0x02, 0x03 };

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(userId, TestUserSub, UserRole.Patient));

    var report = CreateReport(
      reportId,
      patientId: userId,
      uploadedByUserId: userId,
      checksum: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var s3Client = SetupS3GetObject(storedContent);

    var (sut, _) = CreateService(s3Client: s3Client, userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId);

    var act = () => sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("Checksum verification failed*");
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldReturnPresignedUrl_WhenChecksumMatches()
  {
    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var fileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
    var checksum = ComputeSha256Hex(fileContent);
    var expectedUrl = "https://s3.ap-south-1.amazonaws.com/aarogya-test-bucket/reports/signed?token=abc";

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(userId, TestUserSub, UserRole.Patient));

    var report = CreateReport(reportId, patientId: userId, uploadedByUserId: userId, checksum: checksum);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var s3Client = SetupS3GetObject(fileContent);
    SetupPresignedUrl(s3Client, expectedUrl);

    var (sut, _) = CreateService(s3Client: s3Client, userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId, ExpiryMinutes: 10);

    var result = await sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    result.Should().NotBeNull();
    result.ReportId.Should().Be(reportId);
    result.ObjectKey.Should().Be(TestStorageKey);
    result.DownloadUrl.Should().Be(new Uri(expectedUrl));
    result.ChecksumVerified.Should().BeTrue();
    result.ExpiresAt.Should().Be(FixedNow.AddMinutes(10));
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldThrow_WhenFileStorageKeyIsMissing()
  {
    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(userId, TestUserSub, UserRole.Patient));

    var report = new Report
    {
      Id = reportId,
      PatientId = userId,
      UploadedByUserId = userId,
      FileStorageKey = null,
      ChecksumSha256 = "ABC123"
    };

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var (sut, _) = CreateService(userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId);

    var act = () => sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*file storage key*");
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldThrow_WhenChecksumIsMissing()
  {
    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(userId, TestUserSub, UserRole.Patient));

    var report = new Report
    {
      Id = reportId,
      PatientId = userId,
      UploadedByUserId = userId,
      FileStorageKey = TestStorageKey,
      ChecksumSha256 = null
    };

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var (sut, _) = CreateService(userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId);

    var act = () => sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*checksum*missing*");
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldUseDefaultExpiry_WhenExpiryMinutesIsNullAsync()
  {
    var userId = Guid.NewGuid();
    var reportId = Guid.NewGuid();
    var fileContent = new byte[] { 0x01 };
    var checksum = ComputeSha256Hex(fileContent);
    const int defaultExpiryMinutes = 15;

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateUser(userId, TestUserSub, UserRole.Patient));

    var report = CreateReport(reportId, patientId: userId, uploadedByUserId: userId, checksum: checksum);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ReportByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var s3Client = SetupS3GetObject(fileContent);
    SetupPresignedUrl(s3Client, "https://s3.example.com/signed");

    var (sut, _) = CreateService(s3Client: s3Client, userRepository: userRepo, reportRepository: reportRepo);
    var request = new CreateVerifiedReportDownloadRequest(reportId, ExpiryMinutes: null);

    var result = await sut.CreateVerifiedDownloadUrlAsync(TestUserSub, request, CancellationToken.None);

    result.ExpiresAt.Should().Be(FixedNow.AddMinutes(defaultExpiryMinutes));
  }

  private static (S3ReportChecksumVerificationService Sut, Mocks Mocks) CreateService(
    Mock<IAmazonS3>? s3Client = null,
    Mock<IUserRepository>? userRepository = null,
    Mock<IReportRepository>? reportRepository = null,
    AwsOptions? awsOptions = null)
  {
    var s3 = s3Client ?? new Mock<IAmazonS3>();
    var userRepo = userRepository ?? new Mock<IUserRepository>();
    var reportRepo = reportRepository ?? new Mock<IReportRepository>();
    var clock = new Mock<IUtcClock>();
    clock.Setup(x => x.UtcNow).Returns(FixedNow);

    var options = Options.Create(awsOptions ?? new AwsOptions
    {
      UseLocalStack = false,
      S3 = new S3Options
      {
        BucketName = TestBucketName,
        PresignedUrlExpiryMinutes = 15
      }
    });

    var logger = NullLogger<S3ReportChecksumVerificationService>.Instance;

    var sut = new S3ReportChecksumVerificationService(
      s3.Object,
      userRepo.Object,
      reportRepo.Object,
      options,
      clock.Object,
      logger);

    return (sut, new Mocks(s3, userRepo, reportRepo, clock));
  }

  private static User CreateUser(Guid id, string externalAuthId, UserRole role)
  {
    return new User
    {
      Id = id,
      ExternalAuthId = externalAuthId,
      Role = role,
      FirstName = "Test",
      LastName = "User",
      Email = "test@example.com"
    };
  }

  private static Report CreateReport(
    Guid reportId,
    Guid patientId,
    Guid uploadedByUserId,
    string? checksum = null)
  {
    return new Report
    {
      Id = reportId,
      ReportNumber = "RPT-ABCDEF1234",
      PatientId = patientId,
      UploadedByUserId = uploadedByUserId,
      FileStorageKey = TestStorageKey,
      ChecksumSha256 = checksum ?? "DEADBEEF",
      ReportType = ReportType.Other,
      Status = ReportStatus.Processing,
      SourceSystem = "api-upload",
      UploadedAt = FixedNow
    };
  }

  private static Mock<IAmazonS3> SetupS3GetObject(byte[] fileContent)
  {
    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(() =>
      {
        var response = new GetObjectResponse();
        var stream = new MemoryStream(fileContent);
        var responseStreamProperty = typeof(GetObjectResponse)
          .GetProperty(nameof(GetObjectResponse.ResponseStream))!;
        responseStreamProperty.SetValue(response, stream);
        return response;
      });
    return s3Client;
  }

  private static void SetupPresignedUrl(Mock<IAmazonS3> s3Client, string url)
  {
    s3Client
      .Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
      .ReturnsAsync(url);
  }

  private static string ComputeSha256Hex(byte[] content)
  {
    var hash = SHA256.HashData(content);
    return Convert.ToHexString(hash);
  }

  private sealed record Mocks(
    Mock<IAmazonS3> S3Client,
    Mock<IUserRepository> UserRepository,
    Mock<IReportRepository> ReportRepository,
    Mock<IUtcClock> Clock);
}
