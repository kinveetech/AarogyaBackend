namespace Aarogya.Domain.ValueObjects;

public sealed class AccessGrantScope
{
  public bool CanReadReports { get; set; } = true;

  public bool CanDownloadReports { get; set; } = true;

  public ICollection<Guid> AllowedReportIds { get; set; } = [];

  public ICollection<string> AllowedReportTypes { get; set; } = [];
}
