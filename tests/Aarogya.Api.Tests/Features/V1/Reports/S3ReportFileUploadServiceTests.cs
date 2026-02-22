using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class S3ReportFileUploadServiceTests
{
  private static readonly DateTimeOffset FixedNow = new(2026, 2, 21, 12, 0, 0, TimeSpan.Zero);
  private const string TestUserSub = "test-user-sub-001";
  private const string TestBucketName = "aarogya-test-bucket";

  [Fact]
  public async Task UploadAsync_ShouldThrow_WhenFileIsNull()
  {
    var (sut, _) = CreateService();

    var act = () => sut.UploadAsync(TestUserSub, null!, CancellationToken.None);

    await act.Should().ThrowAsync<ArgumentNullException>()
      .WithParameterName("file");
  }

  [Fact]
  public async Task UploadAsync_ShouldThrow_WhenFileIsEmpty()
  {
    var (sut, _) = CreateService();
    var file = CreateFormFile(content: [], fileName: "empty.pdf", contentType: "application/pdf");

    var act = () => sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("Uploaded file is empty.");
  }

  [Fact]
  public async Task UploadAsync_ShouldThrow_WhenFileExceedsMaxSize()
  {
    var (sut, _) = CreateService();
    var oversizedContent = new byte[(50 * 1024 * 1024) + 1];
    var file = CreateFormFile(content: oversizedContent, fileName: "large.pdf", contentType: "application/pdf");

    var act = () => sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*maximum size limit*");
  }

  [Theory]
  [InlineData("application/xml")]
  [InlineData("text/plain")]
  [InlineData("application/octet-stream")]
  [InlineData("image/gif")]
  public async Task UploadAsync_ShouldThrow_WhenContentTypeIsNotAllowed(string contentType)
  {
    var (sut, _) = CreateService();
    var file = CreateFormFile(content: [0x01, 0x02], fileName: "file.txt", contentType: contentType);

    var act = () => sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("Only PDF, JPEG, and PNG files are supported.");
  }

  [Fact]
  public async Task UploadAsync_ShouldThrow_WhenUserIsNotProvisioned()
  {
    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);

    var (sut, _) = CreateService(userRepository: userRepo);
    var file = CreateFormFile(content: [0x25, 0x50, 0x44, 0x46], fileName: "report.pdf", contentType: "application/pdf");

    var act = () => sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    await act.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*not provisioned*");
  }

  [Fact]
  public async Task UploadAsync_ShouldUploadToS3AndPersistReport_WhenFileIsValid()
  {
    var userId = Guid.NewGuid();
    var patient = CreateUser(userId, TestUserSub, UserRole.Patient);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.GetByReportNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutObjectResponse());

    var unitOfWork = new Mock<IUnitOfWork>();
    unitOfWork
      .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(1);

    var (sut, _) = CreateService(
      s3Client: s3Client,
      userRepository: userRepo,
      reportRepository: reportRepo,
      unitOfWork: unitOfWork);

    var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
    var file = CreateFormFile(content: pdfContent, fileName: "blood-test.pdf", contentType: "application/pdf");

    var result = await sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    result.Should().NotBeNull();
    result.ReportId.Should().NotBe(Guid.Empty);
    result.ReportNumber.Should().NotBeNullOrWhiteSpace();
    result.ObjectKey.Should().StartWith("reports/");
    result.ContentType.Should().Be("application/pdf");
    result.SizeBytes.Should().Be(pdfContent.Length);
    result.ChecksumSha256.Should().NotBeNullOrWhiteSpace();
    result.UploadedAt.Should().Be(FixedNow);

    s3Client.Verify(
      x => x.PutObjectAsync(
        It.Is<PutObjectRequest>(r =>
          r.BucketName == TestBucketName &&
          r.Key.StartsWith("reports/") &&
          r.ContentType == "application/pdf"),
        It.IsAny<CancellationToken>()),
      Times.Once);

    reportRepo.Verify(
      x => x.AddAsync(
        It.Is<Report>(r =>
          r.PatientId == userId &&
          r.UploadedByUserId == userId &&
          r.Status == ReportStatus.Processing &&
          r.SourceSystem == "api-upload"),
        It.IsAny<CancellationToken>()),
      Times.Once);

    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task UploadAsync_ShouldResolveRadiologyType_WhenContentTypeIsImage()
  {
    var userId = Guid.NewGuid();
    var patient = CreateUser(userId, TestUserSub, UserRole.Patient);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.GetByReportNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    Report? capturedReport = null;
    reportRepo
      .Setup(x => x.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
      .Callback<Report, CancellationToken>((r, _) => capturedReport = r);

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutObjectResponse());

    var unitOfWork = new Mock<IUnitOfWork>();
    unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var (sut, _) = CreateService(
      s3Client: s3Client,
      userRepository: userRepo,
      reportRepository: reportRepo,
      unitOfWork: unitOfWork);

    var file = CreateFormFile(content: [0xFF, 0xD8, 0xFF], fileName: "xray.jpeg", contentType: "image/jpeg");

    await sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    capturedReport.Should().NotBeNull();
    capturedReport!.ReportType.Should().Be(ReportType.Radiology);
  }

  [Fact]
  public async Task UploadAsync_ShouldResolveOtherType_WhenContentTypeIsPdf()
  {
    var userId = Guid.NewGuid();
    var patient = CreateUser(userId, TestUserSub, UserRole.Patient);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.GetByReportNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    Report? capturedReport = null;
    reportRepo
      .Setup(x => x.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
      .Callback<Report, CancellationToken>((r, _) => capturedReport = r);

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutObjectResponse());

    var unitOfWork = new Mock<IUnitOfWork>();
    unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var (sut, _) = CreateService(
      s3Client: s3Client,
      userRepository: userRepo,
      reportRepository: reportRepo,
      unitOfWork: unitOfWork);

    var file = CreateFormFile(content: [0x25, 0x50, 0x44, 0x46], fileName: "lab.pdf", contentType: "application/pdf");

    await sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    capturedReport.Should().NotBeNull();
    capturedReport!.ReportType.Should().Be(ReportType.Other);
  }

  [Theory]
  [InlineData("application/pdf")]
  [InlineData("image/jpeg")]
  [InlineData("image/png")]
  public async Task UploadAsync_ShouldAcceptAllAllowedContentTypes(string contentType)
  {
    var userId = Guid.NewGuid();
    var patient = CreateUser(userId, TestUserSub, UserRole.Patient);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.GetByReportNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutObjectResponse());

    var unitOfWork = new Mock<IUnitOfWork>();
    unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var (sut, _) = CreateService(
      s3Client: s3Client,
      userRepository: userRepo,
      reportRepository: reportRepo,
      unitOfWork: unitOfWork);

    var file = CreateFormFile(content: [0x01, 0x02, 0x03], fileName: "report.bin", contentType: contentType);

    var result = await sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    result.ContentType.Should().Be(contentType);
  }

  [Fact]
  public async Task UploadAsync_ShouldStoreChecksumInMetadata_WhenUploadSucceeds()
  {
    var userId = Guid.NewGuid();
    var patient = CreateUser(userId, TestUserSub, UserRole.Patient);

    var userRepo = new Mock<IUserRepository>();
    userRepo
      .Setup(x => x.GetByExternalAuthIdAsync(TestUserSub, It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var reportRepo = new Mock<IReportRepository>();
    reportRepo
      .Setup(x => x.GetByReportNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);

    PutObjectRequest? capturedRequest = null;
    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .Callback<PutObjectRequest, CancellationToken>((r, _) => capturedRequest = r)
      .ReturnsAsync(new PutObjectResponse());

    var unitOfWork = new Mock<IUnitOfWork>();
    unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var (sut, _) = CreateService(
      s3Client: s3Client,
      userRepository: userRepo,
      reportRepository: reportRepo,
      unitOfWork: unitOfWork);

    var file = CreateFormFile(content: [0x25, 0x50, 0x44, 0x46], fileName: "test.pdf", contentType: "application/pdf");

    var result = await sut.UploadAsync(TestUserSub, file, CancellationToken.None);

    result.ChecksumSha256.Should().NotBeNullOrWhiteSpace();
    capturedRequest.Should().NotBeNull();
    capturedRequest!.Metadata["sha256"].Should().Be(result.ChecksumSha256);
  }

  private static (S3ReportFileUploadService Sut, Mocks Mocks) CreateService(
    Mock<IAmazonS3>? s3Client = null,
    Mock<IUserRepository>? userRepository = null,
    Mock<IReportRepository>? reportRepository = null,
    Mock<IUnitOfWork>? unitOfWork = null,
    AwsOptions? awsOptions = null)
  {
    var s3 = s3Client ?? new Mock<IAmazonS3>();
    var userRepo = userRepository ?? new Mock<IUserRepository>();
    var reportRepo = reportRepository ?? new Mock<IReportRepository>();
    var uow = unitOfWork ?? new Mock<IUnitOfWork>();
    var clock = new Mock<IUtcClock>();
    clock.Setup(x => x.UtcNow).Returns(FixedNow);

    var options = Options.Create(awsOptions ?? new AwsOptions
    {
      S3 = new S3Options
      {
        BucketName = TestBucketName,
        PresignedUrlExpiryMinutes = 15
      }
    });

    var sut = new S3ReportFileUploadService(
      s3.Object,
      userRepo.Object,
      reportRepo.Object,
      uow.Object,
      options,
      clock.Object);

    return (sut, new Mocks(s3, userRepo, reportRepo, uow, clock));
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

  private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
  {
    var stream = new MemoryStream(content);
    return new FormFile(stream, 0, content.Length, "file", fileName)
    {
      Headers = new HeaderDictionary(),
      ContentType = contentType
    };
  }

  private sealed record Mocks(
    Mock<IAmazonS3> S3Client,
    Mock<IUserRepository> UserRepository,
    Mock<IReportRepository> ReportRepository,
    Mock<IUnitOfWork> UnitOfWork,
    Mock<IUtcClock> Clock);
}
