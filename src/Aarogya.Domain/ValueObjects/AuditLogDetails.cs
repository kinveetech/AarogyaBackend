namespace Aarogya.Domain.ValueObjects;

public sealed class AuditLogDetails
{
  public string? Summary { get; set; }

  public Dictionary<string, string> Data { get; set; } = [];
}
