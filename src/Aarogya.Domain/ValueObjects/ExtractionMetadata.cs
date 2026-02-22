namespace Aarogya.Domain.ValueObjects;

public sealed class ExtractionMetadata
{
  public string? ExtractionMethod { get; set; }

  public string? StructuringModel { get; set; }

  public int ExtractedParameterCount { get; set; }

  public double? OverallConfidence { get; set; }

  public string? RawExtractedText { get; set; }

  public int? PageCount { get; set; }

  public DateTimeOffset? ExtractedAt { get; set; }

  public DateTimeOffset? StructuredAt { get; set; }

  public string? ErrorMessage { get; set; }

  public int AttemptCount { get; set; }

  public Dictionary<string, string> ProviderMetadata { get; set; } = [];
}
