using Aarogya.Api.Authentication;
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

namespace Aarogya.Api.Tests;

public sealed class ReportServiceTests
{
  [Fact]
  public async Task AddForUserAsync_ShouldCreateReportWithMetadataAndS3ReferenceAsync()
  {
    var uploader = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Seed",
      LastName = "Patient",
      Email = "seed.patient@aarogya.dev"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(uploader);

    Report? createdReport = null;
    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.GetByReportNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Report?)null);
    reportRepository
      .Setup(x => x.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
      .Callback<Report, CancellationToken>((report, _) => createdReport = report)
      .Returns(Task.CompletedTask);

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateObjectMetadataResponse("application/pdf", 2048));

    var unitOfWork = new Mock<IUnitOfWork>();

    var service = new ReportService(
      s3Client.Object,
      userRepository.Object,
      Mock.Of<IAccessGrantRepository>(),
      reportRepository.Object,
      unitOfWork.Object,
      Options.Create(CreateAwsOptions()),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)));

    var response = await service.AddForUserAsync("seed-PATIENT-1", CreateRequest(), CancellationToken.None);

    createdReport.Should().NotBeNull();
    createdReport!.PatientId.Should().Be(uploader.Id);
    createdReport.UploadedByUserId.Should().Be(uploader.Id);
    createdReport.FileStorageKey.Should().Be("reports/seed-PATIENT-1/2026/02/report.pdf");
    createdReport.Parameters.Should().HaveCount(1);
    createdReport.Metadata.Tags.Should().ContainKey("lab-name");
    createdReport.Results.Parameters.Should().HaveCount(1);

    response.ReportId.Should().Be(createdReport.Id);
    response.Status.Should().Be("uploaded");

    unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task AddForUserAsync_ShouldRequirePatientSub_ForLabTechnicianAsync()
  {
    var uploader = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-LABTECH-1",
      Role = UserRole.LabTechnician,
      FirstName = "Lab",
      LastName = "Tech",
      Email = "seed.lab@aarogya.dev"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-LABTECH-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(uploader);

    var service = new ReportService(
      Mock.Of<IAmazonS3>(),
      userRepository.Object,
      Mock.Of<IAccessGrantRepository>(),
      Mock.Of<IReportRepository>(),
      Mock.Of<IUnitOfWork>(),
      Options.Create(CreateAwsOptions()),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var request = CreateRequest() with { PatientSub = null };

    var action = async () => await service.AddForUserAsync("seed-LABTECH-1", request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*PatientSub is required*");
  }

  private static CreateReportRequest CreateRequest()
  {
    return new CreateReportRequest(
      ReportType: "blood_test",
      ObjectKey: "reports/seed-PATIENT-1/2026/02/report.pdf",
      LabName: "Aarogya Diagnostics",
      LabCode: "AAR-001",
      CollectedAt: new DateTimeOffset(2026, 2, 19, 9, 0, 0, TimeSpan.Zero),
      ReportedAt: new DateTimeOffset(2026, 2, 20, 8, 0, 0, TimeSpan.Zero),
      Notes: "Fasting sample",
      PatientSub: null,
      Parameters:
      [
        new CreateReportParameterRequest(
          "HGB",
          "Hemoglobin",
          13.4m,
          null,
          "g/dL",
          "12.0-16.0",
          false,
          new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
          {
            ["method"] = "automated"
          })
      ],
      SourceSystem: "api-v1");
  }

  private static GetObjectMetadataResponse CreateObjectMetadataResponse(string contentType, long contentLength)
  {
    var response = new GetObjectMetadataResponse
    {
      ContentLength = contentLength
    };

    response.Headers.ContentType = contentType;
    return response;
  }

  private static AwsOptions CreateAwsOptions()
  {
    return new AwsOptions
    {
      UseLocalStack = true,
      S3 = new S3Options
      {
        BucketName = "aarogya-dev",
        PresignedUrlExpiryMinutes = 15
      }
    };
  }

  private sealed class FixedUtcClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
