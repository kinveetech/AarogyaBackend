using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IReportFileUploadService
{
  public Task<ReportUploadResponse> UploadAsync(
    string userSub,
    IFormFile file,
    CancellationToken cancellationToken = default);
}
