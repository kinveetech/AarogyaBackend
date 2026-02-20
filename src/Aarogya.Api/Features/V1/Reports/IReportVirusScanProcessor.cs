namespace Aarogya.Api.Features.V1.Reports;

internal interface IReportVirusScanProcessor
{
  public Task ProcessUploadAsync(
    S3UploadEventRecord record,
    CancellationToken cancellationToken = default);
}
