using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class ReportsControllerTests
{
  [Fact]
  public async Task ListReportsAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: Mock.Of<IReportService>(),
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.ListReportsAsync(new ReportListQueryRequest(), CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task GetReportDetailAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: Mock.Of<IReportService>(),
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.GetReportDetailAsync(Guid.NewGuid(), CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task GetReportDetailAsync_ShouldReturnOk_WhenServiceReturnsReportAsync()
  {
    var response = new ReportDetailResponse(
      Guid.NewGuid(),
      "RPT-ABC123DEFG",
      "blood_test",
      "uploaded",
      new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
      "Aarogya Diagnostics",
      "AAR-001",
      new DateTimeOffset(2026, 2, 19, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
      "Fasting sample",
      [
        new ReportDetailParameterResponse("HGB", "Hemoglobin", 13.4m, null, "g/dL", "12-16", false)
      ],
      new ReportSignedDownloadUrlResponse(
        "reports/seed-PATIENT-1/2026/02/report.pdf",
        new Uri("https://example.com/signed-download"),
        new DateTimeOffset(2026, 2, 20, 0, 15, 0, TimeSpan.Zero),
        "s3"));

    var reportService = new Mock<IReportService>();
    reportService
      .Setup(x => x.GetDetailForUserAsync("seed-PATIENT-1", response.ReportId, It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: reportService.Object,
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.GetReportDetailAsync(response.ReportId, CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task ListReportsAsync_ShouldReturnOk_WithPagedResponseAsync()
  {
    var expected = new ReportListResponse(
      Page: 1,
      PageSize: 20,
      TotalCount: 1,
      Items:
      [
        new ReportSummaryResponse(
          Guid.NewGuid(),
          "blood_test - Aarogya Diagnostics",
          "uploaded",
          new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero))
      ]);

    var reportService = new Mock<IReportService>();
    reportService
      .Setup(x => x.GetForUserAsync("seed-PATIENT-1", It.IsAny<ReportListQueryRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(expected);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: reportService.Object,
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.ListReportsAsync(new ReportListQueryRequest(), CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(expected);
  }

  [Fact]
  public async Task CreateReportAsync_ShouldReturnForbidden_ForDoctorRoleAsync()
  {
    var reportService = new Mock<IReportService>();

    var controller = CreateController(
      user: CreateUser("seed-DOCTOR-1", ClaimTypes.Role, "Doctor"),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: reportService.Object,
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.CreateReportAsync(CreateReportRequest(), CancellationToken.None);

    result.Should().BeOfType<ForbidResult>();
  }

  [Fact]
  public async Task CreateReportAsync_ShouldReturnBadRequest_WhenServiceThrowsValidationErrorAsync()
  {
    var reportService = new Mock<IReportService>();
    reportService
      .Setup(x => x.AddForUserAsync(It.IsAny<string>(), It.IsAny<CreateReportRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Object key does not belong to the uploader scope."));

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1", ClaimTypes.Role, "Patient"),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: reportService.Object,
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.CreateReportAsync(CreateReportRequest(), CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  [Fact]
  public async Task UploadReportFileAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: Mock.Of<IReportService>(),
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.UploadReportFileAsync(CreateFormFile("application/pdf", "report.pdf"), CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task UploadReportFileAsync_ShouldReturnBadRequest_WhenUploadValidationFailsAsync()
  {
    var uploadService = new Mock<IReportFileUploadService>();
    uploadService
      .Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Only PDF, JPEG, and PNG files are supported."));

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      uploadService: uploadService.Object,
      reportService: Mock.Of<IReportService>(),
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.UploadReportFileAsync(CreateFormFile("text/plain", "report.txt"), CancellationToken.None);

    var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
    badRequest.Value.Should().BeOfType<ValidationErrorResponse>();
  }

  [Fact]
  public async Task UploadReportFileAsync_ShouldReturnCreated_WhenUploadSucceedsAsync()
  {
    var expected = new ReportUploadResponse(
      Guid.NewGuid(),
      "RPT-ABC123DEF0",
      "reports/seed-PATIENT-1/2026/02/20/object.pdf",
      "application/pdf",
      1024,
      "ABCDEF1234",
      new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero));

    var uploadService = new Mock<IReportFileUploadService>();
    uploadService
      .Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(expected);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      uploadService: uploadService.Object,
      reportService: Mock.Of<IReportService>(),
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.UploadReportFileAsync(CreateFormFile("application/pdf", "report.pdf"), CancellationToken.None);

    var created = result.Should().BeOfType<CreatedResult>().Subject;
    created.Value.Should().BeEquivalentTo(expected);
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: Mock.Of<IReportService>(),
      checksumService: Mock.Of<IReportChecksumVerificationService>());

    var result = await controller.CreateVerifiedDownloadUrlAsync(
      new CreateVerifiedReportDownloadRequest(Guid.NewGuid()),
      CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldReturnNotFound_WhenReportIsMissingAsync()
  {
    var checksumService = new Mock<IReportChecksumVerificationService>();
    checksumService
      .Setup(x => x.CreateVerifiedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<CreateVerifiedReportDownloadRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new KeyNotFoundException("Report not found."));

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: Mock.Of<IReportService>(),
      checksumService: checksumService.Object);

    var result = await controller.CreateVerifiedDownloadUrlAsync(
      new CreateVerifiedReportDownloadRequest(Guid.NewGuid()),
      CancellationToken.None);

    result.Should().BeOfType<NotFoundObjectResult>();
  }

  [Fact]
  public async Task CreateVerifiedDownloadUrlAsync_ShouldReturnOk_WhenChecksumMatchesAsync()
  {
    var response = new VerifiedReportDownloadResponse(
      Guid.NewGuid(),
      "reports/seed-PATIENT-1/2026/02/20/abc.pdf",
      new Uri("https://example.com/download"),
      DateTimeOffset.UtcNow.AddMinutes(15),
      true);

    var checksumService = new Mock<IReportChecksumVerificationService>();
    checksumService
      .Setup(x => x.CreateVerifiedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<CreateVerifiedReportDownloadRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(
      user: CreateUser("seed-PATIENT-1"),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: Mock.Of<IReportService>(),
      checksumService: checksumService.Object);

    var result = await controller.CreateVerifiedDownloadUrlAsync(
      new CreateVerifiedReportDownloadRequest(response.ReportId),
      CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  private static ReportsController CreateController(
    ClaimsPrincipal user,
    IReportFileUploadService uploadService,
    IReportService reportService,
    IReportChecksumVerificationService checksumService)
  {
    var consentService = new Mock<IConsentService>();
    consentService
      .Setup(x => x.EnsureGrantedAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    return new ReportsController(reportService, uploadService, checksumService, consentService.Object)
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext
        {
          User = user
        }
      }
    };
  }

  private static ClaimsPrincipal CreateUser(
    string sub,
    string? additionalClaimType = null,
    string? additionalClaimValue = null)
  {
    var claims = new List<Claim>
    {
      new("sub", sub)
    };

    if (!string.IsNullOrWhiteSpace(additionalClaimType) && !string.IsNullOrWhiteSpace(additionalClaimValue))
    {
      claims.Add(new Claim(additionalClaimType, additionalClaimValue));
    }

    return new ClaimsPrincipal(new ClaimsIdentity(
      claims,
      "TestAuth"));
  }

  private static CreateReportRequest CreateReportRequest()
  {
    return new CreateReportRequest(
      ReportType: "blood_test",
      ObjectKey: "reports/seed-PATIENT-1/2026/02/report.pdf",
      LabName: "Aarogya Diagnostics",
      LabCode: "AAR-001",
      CollectedAt: new DateTimeOffset(2026, 2, 19, 0, 0, 0, TimeSpan.Zero),
      ReportedAt: new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero),
      Notes: "Test",
      PatientSub: null,
      Parameters:
      [
        new CreateReportParameterRequest(
          "HGB",
          "Hemoglobin",
          13.4m,
          null,
          "g/dL",
          "12-16",
          false)
      ],
      SourceSystem: "api-v1");
  }

  private static FormFile CreateFormFile(string contentType, string fileName)
  {
    var stream = new MemoryStream(new byte[1024]);
    return new FormFile(stream, 0, stream.Length, "file", fileName)
    {
      Headers = new HeaderDictionary(),
      ContentType = contentType
    };
  }
}
