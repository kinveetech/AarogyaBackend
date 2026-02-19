namespace Aarogya.Domain.ValueObjects;

public sealed class ReportMetadata
{
  public string? SourceSystem { get; set; }

  public Dictionary<string, string> Tags { get; set; } = [];
}
