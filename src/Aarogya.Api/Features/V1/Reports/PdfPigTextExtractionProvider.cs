using System.Text;
using UglyToad.PdfPig;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class PdfPigTextExtractionProvider(ILogger<PdfPigTextExtractionProvider> logger) : ITextExtractionProvider
{
  public Task<TextExtractionResult> ExtractTextAsync(
    Stream pdfStream,
    string objectKey,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(pdfStream);

    logger.LogInformation(
      "Starting PdfPig text extraction for object {ObjectKey}",
      objectKey);

    var bytes = ReadStreamToBytes(pdfStream);
    using var document = PdfDocument.Open(bytes);

    var pageCount = document.NumberOfPages;
    var textBuilder = new StringBuilder();
    var metadata = new Dictionary<string, string>();

    if (document.Information.Producer is { } producer)
    {
      metadata["producer"] = producer;
    }

    if (document.Information.Creator is { } creator)
    {
      metadata["creator"] = creator;
    }

    metadata["page_count"] = pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    for (var i = 1; i <= pageCount; i++)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var page = document.GetPage(i);
      var pageText = page.Text;

      if (!string.IsNullOrWhiteSpace(pageText))
      {
        textBuilder.AppendLine(pageText);
      }
    }

    var extractedText = textBuilder.ToString().Trim();

    logger.LogInformation(
      "PdfPig extracted {CharCount} characters from {PageCount} pages for object {ObjectKey}",
      extractedText.Length,
      pageCount,
      objectKey);

    var result = new TextExtractionResult(
      extractedText,
      "pdfpig",
      pageCount,
      UsedOcr: false,
      metadata);

    return Task.FromResult(result);
  }

  private static byte[] ReadStreamToBytes(Stream stream)
  {
    if (stream is MemoryStream memoryStream)
    {
      return memoryStream.ToArray();
    }

    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    return ms.ToArray();
  }
}
