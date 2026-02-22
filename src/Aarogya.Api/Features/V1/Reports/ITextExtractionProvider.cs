namespace Aarogya.Api.Features.V1.Reports;

internal interface ITextExtractionProvider
{
  public Task<TextExtractionResult> ExtractTextAsync(
    Stream pdfStream,
    string objectKey,
    CancellationToken cancellationToken = default);
}

internal sealed record TextExtractionResult(
  string ExtractedText,
  string Method,
  int PageCount,
  bool UsedOcr,
  Dictionary<string, string> ProviderMetadata);
