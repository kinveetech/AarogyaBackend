using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IReportChecksumVerificationService
{
  public Task<VerifiedReportDownloadResponse> CreateVerifiedDownloadUrlAsync(
    string userSub,
    CreateVerifiedReportDownloadRequest request,
    CancellationToken cancellationToken = default);
}
