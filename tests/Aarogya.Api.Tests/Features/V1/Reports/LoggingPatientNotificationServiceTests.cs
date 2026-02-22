using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class LoggingPatientNotificationServiceTests
{
  [Fact]
  public async Task NotifyReportUploadedAsync_ShouldDelegateToTransactionalEmailServiceAsync()
  {
    var emailService = new Mock<ITransactionalEmailNotificationService>();
    emailService
      .Setup(x => x.SendReportUploadedAsync(
        It.IsAny<User>(),
        It.IsAny<Report>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var sut = new LoggingPatientNotificationService(
      NullLogger<LoggingPatientNotificationService>.Instance,
      emailService.Object);

    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-1",
      Role = UserRole.Patient,
      FirstName = "Jane",
      LastName = "Doe",
      Email = "jane@example.com"
    };

    var report = new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = "RPT-ABCDEF1234",
      PatientId = patient.Id,
      UploadedByUserId = patient.Id,
      ReportType = ReportType.Other,
      Status = ReportStatus.Processing,
      UploadedAt = DateTimeOffset.UtcNow
    };

    await sut.NotifyReportUploadedAsync(patient, report, CancellationToken.None);

    emailService.Verify(
      x => x.SendReportUploadedAsync(patient, report, It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task NotifyReportUploadedAsync_ShouldPassCorrectPatientAndReportAsync()
  {
    User? capturedPatient = null;
    Report? capturedReport = null;

    var emailService = new Mock<ITransactionalEmailNotificationService>();
    emailService
      .Setup(x => x.SendReportUploadedAsync(
        It.IsAny<User>(),
        It.IsAny<Report>(),
        It.IsAny<CancellationToken>()))
      .Callback<User, Report, CancellationToken>((p, r, _) =>
      {
        capturedPatient = p;
        capturedReport = r;
      })
      .Returns(Task.CompletedTask);

    var sut = new LoggingPatientNotificationService(
      NullLogger<LoggingPatientNotificationService>.Instance,
      emailService.Object);

    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-2",
      Role = UserRole.Patient,
      FirstName = "John",
      LastName = "Smith",
      Email = "john@example.com"
    };

    var report = new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = "RPT-TEST123456",
      PatientId = patient.Id,
      UploadedByUserId = patient.Id,
      ReportType = ReportType.Radiology,
      Status = ReportStatus.Processing,
      UploadedAt = DateTimeOffset.UtcNow
    };

    await sut.NotifyReportUploadedAsync(patient, report, CancellationToken.None);

    capturedPatient.Should().BeSameAs(patient);
    capturedReport.Should().BeSameAs(report);
  }

  [Fact]
  public async Task NotifyReportUploadedAsync_ShouldNotThrow_WhenEmailServiceSucceedsAsync()
  {
    var emailService = new Mock<ITransactionalEmailNotificationService>();
    emailService
      .Setup(x => x.SendReportUploadedAsync(
        It.IsAny<User>(),
        It.IsAny<Report>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var sut = new LoggingPatientNotificationService(
      NullLogger<LoggingPatientNotificationService>.Instance,
      emailService.Object);

    var patient = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = "seed-PATIENT-3",
      Role = UserRole.Patient,
      FirstName = "Test",
      LastName = "User",
      Email = "test@example.com"
    };

    var report = new Report
    {
      Id = Guid.NewGuid(),
      ReportNumber = "RPT-SMOKE12345",
      PatientId = patient.Id,
      UploadedByUserId = patient.Id,
      ReportType = ReportType.Other,
      Status = ReportStatus.Processing,
      UploadedAt = DateTimeOffset.UtcNow
    };

    var act = async () => await sut.NotifyReportUploadedAsync(patient, report, CancellationToken.None);
    await act.Should().NotThrowAsync();
  }
}
