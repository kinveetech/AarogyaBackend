using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by tests and DI composition.")]
public interface IReportVirusScanner
{
  public Task<VirusScanResult> ScanObjectAsync(
    string bucketName,
    string objectKey,
    CancellationToken cancellationToken = default);

  public Task RefreshDefinitionsAsync(CancellationToken cancellationToken = default);
}

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by tests and DI composition.")]
public sealed record VirusScanResult(
  bool IsInfected,
  string Engine,
  string? Signature = null);
