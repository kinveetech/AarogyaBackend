namespace Aarogya.Api.Features.V1.Reports;

internal interface IReportParameterExtractor
{
  public Task<StructuredExtractionResult> ExtractParametersAsync(
    string extractedText,
    string? reportType,
    CancellationToken cancellationToken = default);
}
