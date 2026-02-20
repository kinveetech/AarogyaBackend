using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IReportService
{
  public Task<IReadOnlyList<ReportSummaryResponse>> GetForUserAsync(string userSub, CancellationToken cancellationToken = default);

  public Task<ReportSummaryResponse> AddForUserAsync(string userSub, CreateReportRequest request, CancellationToken cancellationToken = default);

  public Task<ReportSignedUploadUrlResponse> GetSignedUploadUrlAsync(
    string userSub,
    CreateReportUploadUrlRequest request,
    CancellationToken cancellationToken = default);

  public Task<ReportSignedDownloadUrlResponse> GetSignedDownloadUrlAsync(
    string userSub,
    CreateReportDownloadUrlRequest request,
    CancellationToken cancellationToken = default);
}
