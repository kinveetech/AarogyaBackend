using Aarogya.Api.Authentication;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class ReportExtractionService(
  IUserRepository userRepository,
  IReportRepository reportRepository,
  IAccessGrantRepository accessGrantRepository,
  IReportPdfExtractionProcessor extractionProcessor,
  IUtcClock clock) : IReportExtractionService
{
  public async Task<ExtractionStatusResponse?> GetExtractionStatusAsync(
    string userSub,
    Guid reportId,
    CancellationToken cancellationToken = default)
  {
    var report = await GetAuthorizedReportAsync(userSub, reportId, cancellationToken);

    if (report.Extraction is null)
    {
      return null;
    }

    return MapExtractionStatus(report);
  }

  public async Task TriggerExtractionAsync(
    string userSub,
    Guid reportId,
    bool forceReprocess = false,
    CancellationToken cancellationToken = default)
  {
    var report = await GetAuthorizedReportAsync(userSub, reportId, cancellationToken);

    if (string.IsNullOrEmpty(report.FileStorageKey))
    {
      throw new InvalidOperationException("Report has no uploaded file.");
    }

    if (!forceReprocess && report.Status is not (ReportStatus.Clean or ReportStatus.ExtractionFailed))
    {
      throw new InvalidOperationException(
        $"Report is in status '{report.Status}' and cannot be extracted. " +
        "Use forceReprocess=true to re-extract.");
    }

    await extractionProcessor.ProcessReportAsync(reportId, forceReprocess, cancellationToken);
  }

  private async Task<Report> GetAuthorizedReportAsync(
    string userSub,
    Guid reportId,
    CancellationToken cancellationToken)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new InvalidOperationException("Authenticated user is not provisioned in the database.");

    var report = await reportRepository.FirstOrDefaultAsync(
      new ReportByIdSpecification(reportId),
      cancellationToken)
      ?? throw new KeyNotFoundException("Report not found.");

    var canAccess = await CanAccessReportAsync(user, report, cancellationToken);
    if (!canAccess)
    {
      throw new UnauthorizedAccessException("You do not have access to this report.");
    }

    return report;
  }

  private async Task<bool> CanAccessReportAsync(
    User user,
    Report report,
    CancellationToken cancellationToken)
  {
    if (user.Role == UserRole.Patient)
    {
      return report.PatientId == user.Id;
    }

    if (user.Role == UserRole.LabTechnician)
    {
      return report.UploadedByUserId == user.Id;
    }

    if (user.Role != UserRole.Doctor)
    {
      return false;
    }

    var now = clock.UtcNow;
    var grants = await accessGrantRepository.ListAsync(
      new ActiveAccessGrantsForDoctorSpecification(user.Id, now),
      cancellationToken);

    return grants.Any(g => g.PatientId == report.PatientId);
  }

  private static ExtractionStatusResponse MapExtractionStatus(Report report)
  {
    var extraction = report.Extraction!;
    return new ExtractionStatusResponse(
      report.Id,
      report.Status.ToString(),
      extraction.ExtractionMethod,
      extraction.StructuringModel,
      extraction.ExtractedParameterCount,
      extraction.OverallConfidence,
      extraction.PageCount,
      extraction.ExtractedAt,
      extraction.ErrorMessage,
      extraction.AttemptCount);
  }
}
