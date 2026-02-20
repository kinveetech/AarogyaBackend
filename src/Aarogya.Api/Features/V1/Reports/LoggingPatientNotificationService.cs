using Aarogya.Domain.Entities;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class LoggingPatientNotificationService(ILogger<LoggingPatientNotificationService> logger)
  : IPatientNotificationService
{
  public Task NotifyReportUploadedAsync(
    User patient,
    Report report,
    CancellationToken cancellationToken = default)
  {
    logger.LogInformation(
      "Patient notification queued for report upload. patientId={PatientId}, reportId={ReportId}, reportNumber={ReportNumber}",
      patient.Id,
      report.Id,
      report.ReportNumber);
    return Task.CompletedTask;
  }
}
