using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Specifications;

public sealed class CleanReportsAwaitingExtractionSpecification : BaseSpecification<Report>
{
  public CleanReportsAwaitingExtractionSpecification(int batchSize)
    : base(report =>
      report.Status == ReportStatus.Clean
      && !report.IsDeleted
      && report.FileStorageKey != null)
  {
    ApplyOrderBy(report => report.UpdatedAt);
    ApplyPaging(0, batchSize);
  }
}
