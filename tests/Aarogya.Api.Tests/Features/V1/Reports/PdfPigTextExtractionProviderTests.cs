using Aarogya.Api.Features.V1.Reports;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class PdfPigTextExtractionProviderTests
{
  private readonly PdfPigTextExtractionProvider _provider;

  public PdfPigTextExtractionProviderTests()
  {
    var logger = new Mock<ILogger<PdfPigTextExtractionProvider>>();
    _provider = new PdfPigTextExtractionProvider(logger.Object);
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldExtractTextFromDigitalPdfAsync()
  {
    using var pdfStream = CreatePdfWithText("Hemoglobin: 14.5 g/dL\nWBC: 7500 /uL");

    var result = await _provider.ExtractTextAsync(pdfStream, "reports/test.pdf");

    result.ExtractedText.Should().Contain("Hemoglobin");
    result.ExtractedText.Should().Contain("14.5");
    result.Method.Should().Be("pdfpig");
    result.PageCount.Should().Be(1);
    result.UsedOcr.Should().BeFalse();
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldReturnEmptyTextForPdfWithNoTextAsync()
  {
    using var pdfStream = CreateEmptyPdf();

    var result = await _provider.ExtractTextAsync(pdfStream, "reports/empty.pdf");

    result.ExtractedText.Should().BeEmpty();
    result.Method.Should().Be("pdfpig");
    result.PageCount.Should().Be(1);
    result.UsedOcr.Should().BeFalse();
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldReturnMetadataWithPageCountAsync()
  {
    using var pdfStream = CreatePdfWithText("Test content");

    var result = await _provider.ExtractTextAsync(pdfStream, "reports/meta.pdf");

    result.ProviderMetadata.Should().ContainKey("page_count");
    result.ProviderMetadata["page_count"].Should().Be("1");
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldThrowOnNullStreamAsync()
  {
    var act = () => _provider.ExtractTextAsync(null!, "key");

    await act.Should().ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldHandleMemoryStreamDirectlyAsync()
  {
    using var pdfStream = CreatePdfWithText("Direct memory stream test");

    // Ensure we're working with a MemoryStream for the optimization path
    pdfStream.Should().BeOfType<MemoryStream>();

    var result = await _provider.ExtractTextAsync(pdfStream, "reports/memory.pdf");

    result.ExtractedText.Should().Contain("Direct memory stream test");
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldRespectCancellationTokenAsync()
  {
    using var pdfStream = CreatePdfWithText("Cancel test");
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    var act = () => _provider.ExtractTextAsync(pdfStream, "reports/cancel.pdf", cts.Token);

    await act.Should().ThrowAsync<OperationCanceledException>();
  }

  private static MemoryStream CreatePdfWithText(string text)
  {
    var builder = new PdfDocumentBuilder();
    var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
    var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

    var yPosition = 800.0;
    foreach (var line in text.Split('\n'))
    {
      page.AddText(line, 12, new UglyToad.PdfPig.Core.PdfPoint(72, yPosition), font);
      yPosition -= 20;
    }

    var bytes = builder.Build();
    return new MemoryStream(bytes);
  }

  private static MemoryStream CreateEmptyPdf()
  {
    var builder = new PdfDocumentBuilder();
    builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
    var bytes = builder.Build();
    return new MemoryStream(bytes);
  }
}
