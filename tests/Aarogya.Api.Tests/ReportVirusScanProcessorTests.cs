using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.ValueObjects;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class ReportVirusScanProcessorTests
{
  [Fact]
  public async Task ProcessUploadAsync_ShouldSetCleanStatus_WhenScannerReturnsCleanAsync()
  {
    var report = BuildReport();
    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.GetByFileStorageKeyAsync(report.FileStorageKey!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var unitOfWork = new Mock<IUnitOfWork>();
    var scanner = new Mock<IReportVirusScanner>();
    scanner
      .Setup(x => x.ScanObjectAsync("aarogya-dev", report.FileStorageKey!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new VirusScanResult(false, "clamav-mock"));

    var processor = CreateProcessor(reportRepository.Object, unitOfWork.Object, scanner.Object);
    var record = new S3UploadEventRecord("aarogya-dev", report.FileStorageKey!, 12, "ObjectCreated:Put", DateTimeOffset.UtcNow);

    await processor.ProcessUploadAsync(record, CancellationToken.None);

    report.Status.Should().Be(ReportStatus.Clean);
    report.Metadata.Tags["scan-status"].Should().Be("clean");
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
  }

  [Fact]
  public async Task ProcessUploadAsync_ShouldQuarantineAndSetInfectedStatus_WhenScannerReturnsInfectedAsync()
  {
    var report = BuildReport();
    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.GetByFileStorageKeyAsync(report.FileStorageKey!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var unitOfWork = new Mock<IUnitOfWork>();
    var scanner = new Mock<IReportVirusScanner>();
    scanner
      .Setup(x => x.ScanObjectAsync("aarogya-dev", report.FileStorageKey!, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new VirusScanResult(true, "clamav-mock", "EICAR-Test-Signature"));

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new CopyObjectResponse());
    s3Client
      .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DeleteObjectResponse());

    var processor = CreateProcessor(reportRepository.Object, unitOfWork.Object, scanner.Object, s3Client.Object);
    var record = new S3UploadEventRecord("aarogya-dev", report.FileStorageKey!, 12, "ObjectCreated:Put", DateTimeOffset.UtcNow);

    await processor.ProcessUploadAsync(record, CancellationToken.None);

    report.Status.Should().Be(ReportStatus.Infected);
    report.Metadata.Tags["scan-status"].Should().Be("infected");
    report.Metadata.Tags.Should().ContainKey("quarantine-key");
    s3Client.Verify(x => x.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    s3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  private static ReportVirusScanProcessor CreateProcessor(
    IReportRepository reportRepository,
    IUnitOfWork unitOfWork,
    IReportVirusScanner scanner,
    IAmazonS3? s3Client = null)
  {
    var awsOptions = Options.Create(new AwsOptions
    {
      S3 = new S3Options
      {
        BucketName = "aarogya-dev"
      }
    });
    var scanOptions = Options.Create(new VirusScanningOptions
    {
      EnableScanning = true,
      QuarantineBucketName = "aarogya-dev-quarantine",
      QuarantinePrefix = "quarantine"
    });

    return new ReportVirusScanProcessor(
      reportRepository,
      unitOfWork,
      scanner,
      s3Client ?? Mock.Of<IAmazonS3>(),
      awsOptions,
      scanOptions,
      NullLogger<ReportVirusScanProcessor>.Instance);
  }

  private static Report BuildReport()
  {
    return new Report
    {
      Id = Guid.NewGuid(),
      FileStorageKey = "reports/seed-patient/2026/02/20/file.pdf",
      Status = ReportStatus.Processing,
      Metadata = new ReportMetadata
      {
        Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      }
    };
  }
}
