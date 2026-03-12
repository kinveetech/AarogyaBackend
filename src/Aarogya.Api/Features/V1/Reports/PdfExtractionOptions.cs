using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Reports;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Options classes require public visibility for configuration binding.")]
public sealed class PdfExtractionOptions
{
  public const string SectionName = "PdfExtraction";

  public bool EnableExtraction { get; set; } = true;

  public bool EnableAutoExtractionWorker { get; set; } = true;

  [Range(1, 60)]
  public int WorkerIntervalMinutes { get; set; } = 2;

  [Range(1, 50)]
  public int BatchSize { get; set; } = 10;

  [Range(10, 1000)]
  public int MinTextLengthPerPage { get; set; } = 50;

  [Range(1, 5)]
  public int MaxRetryAttempts { get; set; } = 3;

  [RegularExpression("^(ollama|bedrock)$", ErrorMessage = "LlmProvider must be 'ollama' or 'bedrock'.")]
  public string LlmProvider { get; set; } = "ollama";

  [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration binding requires string type.")]
  public string OllamaEndpoint { get; set; } = "http://localhost:11434";

  public string OllamaModelId { get; set; } = "qwen2.5:14b-instruct";

  public string BedrockModelId { get; set; } = "anthropic.claude-sonnet-4-20250514";

  public string? BedrockRegion { get; set; }

  [Range(2, 30)]
  public int LlmRequestTimeoutMinutes { get; set; } = 10;

  [Range(100, 8000)]
  public int MaxTokens { get; set; } = 4096;

  [Range(0.0, 1.0)]
  public double MinConfidenceThreshold { get; set; } = 0.5;

  public bool StoreRawExtractedText { get; set; } = true;
}
