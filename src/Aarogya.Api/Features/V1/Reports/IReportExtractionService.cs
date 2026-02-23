using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IReportExtractionService
{
  public Task<ExtractionStatusResponse?> GetExtractionStatusAsync(
    string userSub,
    Guid reportId,
    CancellationToken cancellationToken = default);

  public Task TriggerExtractionAsync(
    string userSub,
    Guid reportId,
    bool forceReprocess = false,
    CancellationToken cancellationToken = default);
}
