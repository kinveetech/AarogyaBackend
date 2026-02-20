using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
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
  public async Task UploadReportFileAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(
      user: new ClaimsPrincipal(new ClaimsIdentity()),
      uploadService: Mock.Of<IReportFileUploadService>(),
      reportService: Mock.Of<IReportService>());

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
      reportService: Mock.Of<IReportService>());

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
      reportService: Mock.Of<IReportService>());

    var result = await controller.UploadReportFileAsync(CreateFormFile("application/pdf", "report.pdf"), CancellationToken.None);

    var created = result.Should().BeOfType<CreatedResult>().Subject;
    created.Value.Should().BeEquivalentTo(expected);
  }

  private static ReportsController CreateController(
    ClaimsPrincipal user,
    IReportFileUploadService uploadService,
    IReportService reportService)
  {
    return new ReportsController(reportService, uploadService)
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

  private static ClaimsPrincipal CreateUser(string sub)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim("sub", sub)
    ], "TestAuth"));
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
