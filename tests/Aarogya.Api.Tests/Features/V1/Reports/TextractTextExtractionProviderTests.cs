using Aarogya.Api.Features.V1.Reports;
using Amazon.Textract;
using Amazon.Textract.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class TextractTextExtractionProviderTests
{
  private readonly Mock<IAmazonTextract> _textractMock;
  private readonly TextractTextExtractionProvider _provider;

  public TextractTextExtractionProviderTests()
  {
    _textractMock = new Mock<IAmazonTextract>();
    var logger = new Mock<ILogger<TextractTextExtractionProvider>>();
    _provider = new TextractTextExtractionProvider(_textractMock.Object, logger.Object);
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldUseSyncApiForSmallDocumentsAsync()
  {
    var blocks = new List<Block>
    {
      new() { BlockType = BlockType.PAGE, Page = 1 },
      new()
      {
        BlockType = BlockType.LINE,
        Text = "Hemoglobin: 14.5 g/dL",
        Page = 1,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.1f } }
      },
      new()
      {
        BlockType = BlockType.LINE,
        Text = "WBC: 7500 /uL",
        Page = 1,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.2f } }
      }
    };

    _textractMock
      .Setup(x => x.DetectDocumentTextAsync(It.IsAny<DetectDocumentTextRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DetectDocumentTextResponse { Blocks = blocks });

    using var stream = new MemoryStream(new byte[100]);

    var result = await _provider.ExtractTextAsync(stream, "reports/test.pdf");

    result.ExtractedText.Should().Contain("Hemoglobin: 14.5 g/dL");
    result.ExtractedText.Should().Contain("WBC: 7500 /uL");
    result.Method.Should().Be("textract");
    result.PageCount.Should().Be(1);
    result.UsedOcr.Should().BeTrue();
    result.ProviderMetadata.Should().ContainKey("textract_api").WhoseValue.Should().Be("sync");
    result.ProviderMetadata.Should().ContainKey("block_count").WhoseValue.Should().Be("3");
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldAssembleBlocksInReadingOrderAsync()
  {
    var blocks = new List<Block>
    {
      new() { BlockType = BlockType.PAGE, Page = 1 },
      new()
      {
        BlockType = BlockType.LINE,
        Text = "Second line",
        Page = 1,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.5f } }
      },
      new()
      {
        BlockType = BlockType.LINE,
        Text = "First line",
        Page = 1,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.1f } }
      }
    };

    _textractMock
      .Setup(x => x.DetectDocumentTextAsync(It.IsAny<DetectDocumentTextRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DetectDocumentTextResponse { Blocks = blocks });

    using var stream = new MemoryStream(new byte[100]);

    var result = await _provider.ExtractTextAsync(stream, "reports/order.pdf");

    var lines = result.ExtractedText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    lines[0].Should().Be("First line");
    lines[1].Should().Be("Second line");
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldIgnoreNonLineBlocksAsync()
  {
    var blocks = new List<Block>
    {
      new() { BlockType = BlockType.PAGE, Page = 1 },
      new()
      {
        BlockType = BlockType.LINE,
        Text = "Visible text",
        Page = 1,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.1f } }
      },
      new()
      {
        BlockType = BlockType.WORD,
        Text = "Word block should be ignored",
        Page = 1,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.2f } }
      }
    };

    _textractMock
      .Setup(x => x.DetectDocumentTextAsync(It.IsAny<DetectDocumentTextRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DetectDocumentTextResponse { Blocks = blocks });

    using var stream = new MemoryStream(new byte[100]);

    var result = await _provider.ExtractTextAsync(stream, "reports/filter.pdf");

    result.ExtractedText.Should().Be("Visible text");
    result.ExtractedText.Should().NotContain("Word block");
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldCountPagesCorrectlyAsync()
  {
    var blocks = new List<Block>
    {
      new() { BlockType = BlockType.PAGE, Page = 1 },
      new()
      {
        BlockType = BlockType.LINE,
        Text = "Page 1 content",
        Page = 1,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.1f } }
      },
      new() { BlockType = BlockType.PAGE, Page = 2 },
      new()
      {
        BlockType = BlockType.LINE,
        Text = "Page 2 content",
        Page = 2,
        Geometry = new Geometry { BoundingBox = new BoundingBox { Top = 0.1f } }
      }
    };

    _textractMock
      .Setup(x => x.DetectDocumentTextAsync(It.IsAny<DetectDocumentTextRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DetectDocumentTextResponse { Blocks = blocks });

    using var stream = new MemoryStream(new byte[100]);

    var result = await _provider.ExtractTextAsync(stream, "reports/multipage.pdf");

    result.PageCount.Should().Be(2);
    result.ExtractedText.Should().Contain("Page 1 content");
    result.ExtractedText.Should().Contain("Page 2 content");
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldThrowOnNullStreamAsync()
  {
    var act = () => _provider.ExtractTextAsync(null!, "key");

    await act.Should().ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task ExtractTextAsync_ShouldReturnEmptyTextWhenNoLineBlocksAsync()
  {
    var blocks = new List<Block>
    {
      new() { BlockType = BlockType.PAGE, Page = 1 }
    };

    _textractMock
      .Setup(x => x.DetectDocumentTextAsync(It.IsAny<DetectDocumentTextRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DetectDocumentTextResponse { Blocks = blocks });

    using var stream = new MemoryStream(new byte[100]);

    var result = await _provider.ExtractTextAsync(stream, "reports/empty.pdf");

    result.ExtractedText.Should().BeEmpty();
    result.PageCount.Should().Be(1);
  }
}
