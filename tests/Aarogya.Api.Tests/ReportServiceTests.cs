using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
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
      Mock.Of<IAuditLoggingService>(),
      Mock.Of<IPatientNotificationService>(),
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
  public async Task AddForUserAsync_ShouldRequirePatientIdentifier_ForLabTechnicianAsync()
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
      Mock.Of<IAuditLoggingService>(),
      Mock.Of<IPatientNotificationService>(),
      Mock.Of<IUnitOfWork>(),
      Options.Create(CreateAwsOptions()),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var request = CreateRequest() with { PatientSub = null };

    var action = async () => await service.AddForUserAsync("seed-LABTECH-1", request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
      .WithMessage("*PatientSub, PatientPhone, or PatientAadhaar is required*");
  }

  [Fact]
  public async Task AddForUserAsync_ShouldResolvePatientByPhone_AndNotify_WhenLabTechnicianUploadsAsync()
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

    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Seed",
      LastName = "Patient",
      Email = "seed.patient@aarogya.dev",
      Phone = "+919876543210"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-LABTECH-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(uploader);
    userRepository
      .Setup(x => x.GetByPhoneAsync("+919876543210", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

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

    var notificationService = new Mock<IPatientNotificationService>();
    var service = new ReportService(
      s3Client.Object,
      userRepository.Object,
      Mock.Of<IAccessGrantRepository>(),
      reportRepository.Object,
      Mock.Of<IAuditLoggingService>(),
      notificationService.Object,
      Mock.Of<IUnitOfWork>(),
      Options.Create(CreateAwsOptions()),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)));

    var request = CreateRequest() with
    {
      ObjectKey = "reports/seed-LABTECH-1/2026/02/report.pdf",
      PatientSub = null,
      PatientPhone = "+919876543210",
      PatientAadhaar = null
    };

    _ = await service.AddForUserAsync("seed-LABTECH-1", request, CancellationToken.None);

    createdReport.Should().NotBeNull();
    createdReport!.PatientId.Should().Be(patient.Id);
    createdReport.SourceSystem.Should().Be("lab-upload");
    notificationService.Verify(
      x => x.NotifyReportUploadedAsync(
        It.Is<User>(u => u.Id == patient.Id),
        It.IsAny<Report>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetDetailForUserAsync_ShouldReturnDetailAndWriteAuditLog_WhenPatientOwnsReportAsync()
  {
    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Seed",
      LastName = "Patient",
      Email = "seed.patient@aarogya.dev"
    };

    var report = new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = "RPT-ABC123DEFG",
      PatientId = patient.Id,
      UploadedByUserId = patient.Id,
      ReportType = ReportType.BloodTest,
      Status = ReportStatus.Uploaded,
      UploadedAt = new DateTimeOffset(2026, 2, 20, 8, 0, 0, TimeSpan.Zero),
      CreatedAt = new DateTimeOffset(2026, 2, 20, 8, 0, 0, TimeSpan.Zero),
      FileStorageKey = "reports/seed-PATIENT-1/2026/02/report.pdf",
      Metadata = new ReportMetadata
      {
        Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["lab-name"] = "Aarogya Diagnostics",
          ["lab-code"] = "AAR-001"
        }
      },
      Results = new ReportResults
      {
        Notes = "Fasting sample"
      },
      Parameters =
      [
        new ReportParameter
        {
          ParameterCode = "HGB",
          ParameterName = "Hemoglobin",
          MeasuredValueNumeric = 13.4m,
          Unit = "g/dL",
          ReferenceRangeText = "12-16",
          IsAbnormal = false
        }
      ]
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-PATIENT-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(patient);

    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<Report>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var s3Client = new Mock<IAmazonS3>();
    s3Client
      .Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
      .ReturnsAsync("https://example.com/signed-download");

    var auditLoggingService = new Mock<IAuditLoggingService>();
    var unitOfWork = new Mock<IUnitOfWork>();

    var service = new ReportService(
      s3Client.Object,
      userRepository.Object,
      Mock.Of<IAccessGrantRepository>(),
      reportRepository.Object,
      auditLoggingService.Object,
      Mock.Of<IPatientNotificationService>(),
      unitOfWork.Object,
      Options.Create(CreateAwsOptions()),
      new FixedUtcClock(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.Zero)));

    var response = await service.GetDetailForUserAsync("seed-PATIENT-1", report.Id, CancellationToken.None);

    response.ReportId.Should().Be(report.Id);
    response.Download.Provider.Should().Be("s3");
    response.Download.DownloadUrl.Should().Be(new Uri("https://example.com/signed-download"));
    response.Parameters.Should().HaveCount(1);

    auditLoggingService.Verify(
      x => x.LogDataAccessAsync(
        It.IsAny<User>(),
        "report.viewed",
        "report",
        report.Id,
        200,
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetDetailForUserAsync_ShouldThrowUnauthorized_WhenDoctorHasNoActiveGrantAsync()
  {
    var doctor = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-DOCTOR-1",
      Role = UserRole.Doctor,
      FirstName = "Seed",
      LastName = "Doctor",
      Email = "seed.doctor@aarogya.dev"
    };

    var report = new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = "RPT-XYZ9876543",
      PatientId = Guid.NewGuid(),
      UploadedByUserId = Guid.NewGuid(),
      ReportType = ReportType.BloodTest,
      Status = ReportStatus.Uploaded,
      UploadedAt = DateTimeOffset.UtcNow,
      CreatedAt = DateTimeOffset.UtcNow,
      FileStorageKey = "reports/seed-PATIENT-1/2026/02/report.pdf"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(doctor);

    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<Report>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    accessGrantRepository
      .Setup(x => x.ListAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    var service = new ReportService(
      Mock.Of<IAmazonS3>(),
      userRepository.Object,
      accessGrantRepository.Object,
      reportRepository.Object,
      Mock.Of<IAuditLoggingService>(),
      Mock.Of<IPatientNotificationService>(),
      Mock.Of<IUnitOfWork>(),
      Options.Create(CreateAwsOptions()),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.GetDetailForUserAsync("seed-DOCTOR-1", report.Id, CancellationToken.None);

    await action.Should().ThrowAsync<UnauthorizedAccessException>();
  }

  [Fact]
  public async Task GetDetailForUserAsync_ShouldDenyDoctor_WhenGrantDoesNotIncludeReportIdAsync()
  {
    var doctor = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-DOCTOR-1",
      Role = UserRole.Doctor
    };

    var report = new Report
    {
      Id = Guid.NewGuid(),
      PatientId = Guid.NewGuid(),
      UploadedByUserId = Guid.NewGuid(),
      ReportType = ReportType.BloodTest,
      Status = ReportStatus.Uploaded,
      UploadedAt = DateTimeOffset.UtcNow,
      CreatedAt = DateTimeOffset.UtcNow,
      FileStorageKey = "reports/seed-PATIENT-1/2026/02/report.pdf",
      ReportNumber = "RPT-1234567890"
    };

    var userRepository = new Mock<IUserRepository>();
    userRepository
      .Setup(x => x.GetByExternalAuthIdAsync("seed-DOCTOR-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync(doctor);

    var reportRepository = new Mock<IReportRepository>();
    reportRepository
      .Setup(x => x.FirstOrDefaultAsync(It.IsAny<ISpecification<Report>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(report);

    var accessGrantRepository = new Mock<IAccessGrantRepository>();
    accessGrantRepository
      .Setup(x => x.ListAsync(It.IsAny<ISpecification<AccessGrant>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(
      [
        new AccessGrant
        {
          Id = Guid.NewGuid(),
          PatientId = report.PatientId,
          GrantedToUserId = doctor.Id,
          Status = AccessGrantStatus.Active,
          StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
          ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
          Scope = new AccessGrantScope
          {
            CanReadReports = true,
            CanDownloadReports = true,
            AllowedReportIds = [Guid.NewGuid()]
          }
        }
      ]);

    var service = new ReportService(
      Mock.Of<IAmazonS3>(),
      userRepository.Object,
      accessGrantRepository.Object,
      reportRepository.Object,
      Mock.Of<IAuditLoggingService>(),
      Mock.Of<IPatientNotificationService>(),
      Mock.Of<IUnitOfWork>(),
      Options.Create(CreateAwsOptions()),
      new FixedUtcClock(DateTimeOffset.UtcNow));

    var action = async () => await service.GetDetailForUserAsync("seed-DOCTOR-1", report.Id, CancellationToken.None);

    await action.Should().ThrowAsync<UnauthorizedAccessException>();
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
