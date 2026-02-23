using Aarogya.Api.Features.V1.Reports;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Reports;

public sealed class LlmReportParameterExtractorTests
{
  private readonly Mock<IChatClient> _chatClientMock;
  private readonly LlmReportParameterExtractor _extractor;

  public LlmReportParameterExtractorTests()
  {
    _chatClientMock = new Mock<IChatClient>();
    var options = Options.Create(new PdfExtractionOptions
    {
      LlmProvider = "ollama",
      OllamaModelId = "test-model",
      MaxTokens = 4096,
      MinConfidenceThreshold = 0.5
    });
    var logger = new Mock<ILogger<LlmReportParameterExtractor>>();
    _extractor = new LlmReportParameterExtractor(
      _chatClientMock.Object, options, logger.Object);
  }

  [Fact]
  public async Task ExtractParametersAsync_ShouldReturnStructuredResultAsync()
  {
    var json = """
      {
        "parameters": [
          {
            "code": "HGB",
            "name": "Hemoglobin",
            "numericValue": 14.5,
            "textValue": null,
            "unit": "g/dL",
            "referenceRange": "12.0 - 17.5",
            "isAbnormal": false,
            "confidence": 0.95
          }
        ],
        "overallConfidence": 0.95,
        "notes": null
      }
      """;

    SetupChatResponse(json);

    var result = await _extractor.ExtractParametersAsync("Hemoglobin: 14.5 g/dL", "blood_test");

    result.Parameters.Should().HaveCount(1);
    result.Parameters[0].Code.Should().Be("HGB");
    result.Parameters[0].Name.Should().Be("Hemoglobin");
    result.Parameters[0].NumericValue.Should().Be(14.5m);
    result.Parameters[0].Unit.Should().Be("g/dL");
    result.OverallConfidence.Should().Be(0.95);
    result.ModelId.Should().Be("test-model");
  }

  [Fact]
  public async Task ExtractParametersAsync_ShouldThrowOnNullTextAsync()
  {
    var act = () => _extractor.ExtractParametersAsync(null!, "blood_test");

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task ExtractParametersAsync_ShouldThrowOnWhitespaceTextAsync()
  {
    var act = () => _extractor.ExtractParametersAsync("   ", "blood_test");

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task ExtractParametersAsync_ShouldSendSystemAndUserMessagesAsync()
  {
    SetupChatResponse("""{"parameters": [], "overallConfidence": 0.0, "notes": null}""");

    await _extractor.ExtractParametersAsync("Test text", "blood_test");

    _chatClientMock.Verify(x => x.GetResponseAsync(
      It.Is<IEnumerable<ChatMessage>>(msgs =>
        msgs.Count() == 2 &&
        msgs.First().Role == ChatRole.System &&
        msgs.Last().Role == ChatRole.User),
      It.IsAny<ChatOptions>(),
      It.IsAny<CancellationToken>()));
  }

  [Fact]
  public void ParseResponse_ShouldHandleMarkdownCodeBlockAsync()
  {
    var responseText = """
      ```json
      {
        "parameters": [
          {"code": "WBC", "name": "White Blood Cells", "numericValue": 7500, "unit": "/uL", "referenceRange": "4000-11000", "isAbnormal": false, "confidence": 0.9}
        ],
        "overallConfidence": 0.9,
        "notes": null
      }
      ```
      """;

    var result = LlmReportParameterExtractor.ParseResponse(responseText, "test-model", 0.5);

    result.Parameters.Should().HaveCount(1);
    result.Parameters[0].Code.Should().Be("WBC");
  }

  [Fact]
  public void ParseResponse_ShouldHandleJsonWithSurroundingTextAsync()
  {
    var responseText = """
      Here is the extracted data:
      {"parameters": [{"code": "PLT", "name": "Platelets", "numericValue": 250000, "unit": "/uL", "confidence": 0.85}], "overallConfidence": 0.85, "notes": null}
      Hope this helps!
      """;

    var result = LlmReportParameterExtractor.ParseResponse(responseText, "test-model", 0.5);

    result.Parameters.Should().HaveCount(1);
    result.Parameters[0].Code.Should().Be("PLT");
  }

  [Fact]
  public void ParseResponse_ShouldFilterBelowConfidenceThresholdAsync()
  {
    var json = """
      {
        "parameters": [
          {"code": "HGB", "name": "Hemoglobin", "numericValue": 14.5, "confidence": 0.9},
          {"code": "LOW", "name": "Low Confidence", "numericValue": 1.0, "confidence": 0.2}
        ],
        "overallConfidence": 0.55,
        "notes": null
      }
      """;

    var result = LlmReportParameterExtractor.ParseResponse(json, "test-model", 0.5);

    result.Parameters.Should().HaveCount(1);
    result.Parameters[0].Code.Should().Be("HGB");
  }

  [Fact]
  public void ParseResponse_ShouldFilterParametersWithEmptyNameAsync()
  {
    var json = """
      {
        "parameters": [
          {"code": "HGB", "name": "Hemoglobin", "numericValue": 14.5, "confidence": 0.9},
          {"code": "X", "name": "", "numericValue": 0, "confidence": 0.9},
          {"code": "Y", "name": "  ", "numericValue": 0, "confidence": 0.9}
        ],
        "overallConfidence": 0.7,
        "notes": null
      }
      """;

    var result = LlmReportParameterExtractor.ParseResponse(json, "test-model", 0.5);

    result.Parameters.Should().HaveCount(1);
  }

  [Fact]
  public void ParseResponse_ShouldReturnEmptyWhenNullParametersAsync()
  {
    var json = """{"parameters": null, "overallConfidence": 0.0, "notes": null}""";

    var result = LlmReportParameterExtractor.ParseResponse(json, "test-model", 0.5);

    result.Parameters.Should().BeEmpty();
    result.OverallConfidence.Should().Be(0.0);
  }

  [Fact]
  public void ParseResponse_ShouldGenerateCodeFromNameWhenCodeMissingAsync()
  {
    var json = """
      {
        "parameters": [
          {"code": "", "name": "Total Cholesterol", "numericValue": 200, "confidence": 0.8}
        ],
        "overallConfidence": 0.8,
        "notes": null
      }
      """;

    var result = LlmReportParameterExtractor.ParseResponse(json, "test-model", 0.5);

    result.Parameters[0].Code.Should().Be("TC");
  }

  [Fact]
  public void ParseResponse_ShouldGenerateCodeFromSingleWordNameAsync()
  {
    var json = """
      {
        "parameters": [
          {"code": "", "name": "Iron", "numericValue": 80, "confidence": 0.8}
        ],
        "overallConfidence": 0.8,
        "notes": null
      }
      """;

    var result = LlmReportParameterExtractor.ParseResponse(json, "test-model", 0.5);

    result.Parameters[0].Code.Should().Be("IRON");
  }

  [Fact]
  public void ParseResponse_ShouldReturnEmptyOnInvalidJsonAsync()
  {
    var result = LlmReportParameterExtractor.ParseResponse("not json at all", "test-model", 0.5);

    result.Parameters.Should().BeEmpty();
  }

  private void SetupChatResponse(string text)
  {
    var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    _chatClientMock
      .Setup(x => x.GetResponseAsync(
        It.IsAny<IEnumerable<ChatMessage>>(),
        It.IsAny<ChatOptions>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);
  }
}
