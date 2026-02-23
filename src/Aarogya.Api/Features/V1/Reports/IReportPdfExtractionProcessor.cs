namespace Aarogya.Api.Features.V1.Reports;

internal interface IReportPdfExtractionProcessor
{
  public Task ProcessReportAsync(Guid reportId, bool forceReprocess = false, CancellationToken cancellationToken = default);
}
