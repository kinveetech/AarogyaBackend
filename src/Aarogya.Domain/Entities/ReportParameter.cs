using Aarogya.Domain.ValueObjects;

namespace Aarogya.Domain.Entities;

public sealed class ReportParameter
{
  public Guid Id { get; set; }

  public Guid ReportId { get; set; }

  public string ParameterCode { get; set; } = string.Empty;

  public string ParameterName { get; set; } = string.Empty;

  public string? MeasuredValueText { get; set; }

  public decimal? MeasuredValueNumeric { get; set; }

  public string? Unit { get; set; }

  public string? ReferenceRangeText { get; set; }

  public bool? IsAbnormal { get; set; }

  public ReportParameterRaw RawParameter { get; set; } = new();

  public DateTimeOffset CreatedAt { get; set; }

  public Report Report { get; set; } = null!;
}
