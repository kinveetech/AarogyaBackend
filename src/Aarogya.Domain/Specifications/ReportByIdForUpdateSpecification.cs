using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ReportByIdForUpdateSpecification : BaseSpecification<Report>
{
  public ReportByIdForUpdateSpecification(Guid reportId)
    : base(report => report.Id == reportId && !report.IsDeleted)
  {
    AddInclude(report => report.Parameters);
  }
}
