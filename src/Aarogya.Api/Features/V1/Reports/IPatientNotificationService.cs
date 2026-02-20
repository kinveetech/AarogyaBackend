using System.Diagnostics.CodeAnalysis;
using Aarogya.Domain.Entities;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public service constructor injection.")]
public interface IPatientNotificationService
{
  public Task NotifyReportUploadedAsync(
    User patient,
    Report report,
    CancellationToken cancellationToken = default);
}
