using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class ReportHardDeleteHostedServiceTests
{
  [Fact]
  public async Task ExecuteAsync_ShouldHardDeleteDueReportsAsync()
  {
    var report = new Report
    {
      Id = Guid.NewGuid(),
      IsDeleted = true,
      DeletedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
      FileStorageKey = "reports/seed-PATIENT-1/report.pdf",
      ChecksumSha256 = "ABC123"
    };

    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .SetupSequence(x => x.ListDueForHardDeleteAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([report])
      .ReturnsAsync([]);

    var unitOfWork = new Mock<IUnitOfWork>();

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DeleteObjectResponse());

    var service = CreateService(
      reportRepository.Object,
      unitOfWork.Object,
      s3Client.Object,
      new FileDeletionOptions
      {
        EnableHardDeleteWorker = true,
        RetentionDays = 30,
        WorkerIntervalMinutes = 1
      });

    await service.StartAsync(CancellationToken.None);
    await Task.Delay(100);
    await service.StopAsync(CancellationToken.None);

    report.HardDeletedAt.Should().NotBeNull();
    report.FileStorageKey.Should().BeNull();
    report.ChecksumSha256.Should().BeNull();
    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    s3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldSkip_WhenWorkerDisabledAsync()
  {
    var reportRepository = new Mock<IReportRepository>();

    var service = CreateService(
      reportRepository.Object,
      Mock.Of<IUnitOfWork>(),
      Mock.Of<IAmazonS3>(),
      new FileDeletionOptions
      {
        EnableHardDeleteWorker = false,
        RetentionDays = 30,
        WorkerIntervalMinutes = 1
      });

    await service.StartAsync(CancellationToken.None);
    await service.StopAsync(CancellationToken.None);

    reportRepository.Verify(
      x => x.ListDueForHardDeleteAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  private static ReportHardDeleteHostedService CreateService(
    IReportRepository reportRepository,
    IUnitOfWork unitOfWork,
    IAmazonS3 s3Client,
    FileDeletionOptions fileDeletionOptions)
  {
    var awsOptions = Options.Create(new AwsOptions
    {
      S3 = new S3Options
      {
        BucketName = "aarogya-dev"
      }
    });

    return new ReportHardDeleteHostedService(
      reportRepository,
      unitOfWork,
      s3Client,
      awsOptions,
      Options.Create(fileDeletionOptions),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero)),
      NullLogger<ReportHardDeleteHostedService>.Instance);
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
