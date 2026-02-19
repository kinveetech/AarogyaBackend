namespace Aarogya.Domain.ValueObjects;

public sealed class ReportResults
{
  public int ReportVersion { get; set; } = 1;

  public string? Notes { get; set; }

  public ICollection<ReportResultParameter> Parameters { get; set; } = [];
}

public sealed class ReportResultParameter
{
  public string Code { get; set; } = string.Empty;

  public string Name { get; set; } = string.Empty;

  public decimal? Value { get; set; }

  public string? Unit { get; set; }

  public string? ReferenceRange { get; set; }

  public bool? AbnormalFlag { get; set; }
}
