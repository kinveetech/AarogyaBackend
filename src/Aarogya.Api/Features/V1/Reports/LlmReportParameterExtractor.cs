using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class LlmReportParameterExtractor(
  IChatClient chatClient,
  IOptions<PdfExtractionOptions> options,
  ILogger<LlmReportParameterExtractor> logger) : IReportParameterExtractor
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  private const string SystemPrompt = """
    You are a medical laboratory report data extraction assistant.
    Extract all test parameters from the provided lab report text.

    For each parameter, provide:
    - code: A short standardized code (e.g., HGB, WBC, RBC, PLT, FBS, TSH)
    - name: The full parameter name as shown in the report
    - numericValue: The numeric measured value (null if text-only)
    - textValue: The text value if not numeric (e.g., "Positive", "Reactive")
    - unit: The unit of measurement (e.g., g/dL, /uL, mg/dL)
    - referenceRange: The reference range as shown (e.g., "12.0 - 17.5")
    - isAbnormal: true if the value is outside the reference range, false if normal, null if unknown
    - confidence: Your confidence in this extraction (0.0 to 1.0)

    Also provide:
    - overallConfidence: Your overall confidence in the extraction (0.0 to 1.0)
    - notes: Any relevant notes about the extraction

    Respond ONLY with valid JSON matching this exact schema:
    {
      "parameters": [...],
      "overallConfidence": 0.0,
      "notes": "string or null"
    }
    """;

  public async Task<StructuredExtractionResult> ExtractParametersAsync(
    string extractedText,
    string? reportType,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(extractedText);

    var extractionOptions = options.Value;
    var modelId = extractionOptions.LlmProvider == "bedrock"
      ? extractionOptions.BedrockModelId
      : extractionOptions.OllamaModelId;

    logger.LogInformation(
      "Starting LLM parameter extraction using {Provider}/{ModelId}",
      extractionOptions.LlmProvider,
      modelId);

    var userPrompt = BuildUserPrompt(extractedText, reportType);

    var chatMessages = new List<ChatMessage>
    {
      new(ChatRole.System, SystemPrompt),
      new(ChatRole.User, userPrompt)
    };

    var chatOptions = new ChatOptions
    {
      MaxOutputTokens = extractionOptions.MaxTokens,
      Temperature = 0.1f
    };

    var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken);
    var responseText = response.Text ?? string.Empty;

    logger.LogDebug(
      "LLM response received ({ResponseLength} chars)",
      responseText.Length);

    var result = ParseResponse(responseText, modelId, extractionOptions.MinConfidenceThreshold);

    logger.LogInformation(
      "LLM extracted {ParameterCount} parameters with {OverallConfidence:F2} overall confidence",
      result.Parameters.Count,
      result.OverallConfidence);

    return result;
  }

  private static string BuildUserPrompt(string extractedText, string? reportType)
  {
    var typeHint = reportType is not null
      ? $"This is a {reportType} report. "
      : string.Empty;

    return $"""
      {typeHint}Extract all test parameters from the following lab report text:

      ---
      {extractedText}
      ---
      """;
  }

  internal static StructuredExtractionResult ParseResponse(
    string responseText,
    string modelId,
    double minConfidence)
  {
    var jsonText = ExtractJsonFromResponse(responseText);

    LlmExtractionResponse? parsed;
    try
    {
      parsed = JsonSerializer.Deserialize<LlmExtractionResponse>(jsonText, JsonOptions);
    }
    catch (JsonException)
    {
      return new StructuredExtractionResult([], null, 0.0, modelId);
    }

    if (parsed?.Parameters is null)
    {
      return new StructuredExtractionResult([], null, 0.0, modelId);
    }

    var parameters = parsed.Parameters
      .Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Confidence >= minConfidence)
      .Select(p => new ExtractedParameter(
        string.IsNullOrWhiteSpace(p.Code) ? GenerateCode(p.Name) : p.Code,
        p.Name,
        p.NumericValue,
        p.TextValue,
        p.Unit,
        p.ReferenceRange,
        p.IsAbnormal,
        p.Confidence))
      .ToList();

    return new StructuredExtractionResult(
      parameters,
      parsed.Notes,
      parsed.OverallConfidence,
      modelId);
  }

  private static string ExtractJsonFromResponse(string responseText)
  {
    var text = responseText.Trim();

    // Handle markdown code blocks
    if (text.StartsWith("```", StringComparison.Ordinal))
    {
      var startIndex = text.IndexOf('{', StringComparison.Ordinal);
      var endIndex = text.LastIndexOf('}');
      if (startIndex >= 0 && endIndex > startIndex)
      {
        return text[startIndex..(endIndex + 1)];
      }
    }

    // Try to find JSON object directly
    var jsonStart = text.IndexOf('{', StringComparison.Ordinal);
    var jsonEnd = text.LastIndexOf('}');
    if (jsonStart >= 0 && jsonEnd > jsonStart)
    {
      return text[jsonStart..(jsonEnd + 1)];
    }

    return text;
  }

  private static string GenerateCode(string name)
  {
    var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (words.Length == 1)
    {
      return name.Length <= 6
        ? name.ToUpperInvariant()
        : name[..3].ToUpperInvariant();
    }

    return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
  }

  private sealed record LlmExtractionResponse(
    IReadOnlyList<LlmExtractedParameter>? Parameters,
    double OverallConfidence,
    string? Notes);

  private sealed record LlmExtractedParameter(
    string Code,
    string Name,
    decimal? NumericValue,
    string? TextValue,
    string? Unit,
    string? ReferenceRange,
    bool? IsAbnormal,
    double Confidence);
}
