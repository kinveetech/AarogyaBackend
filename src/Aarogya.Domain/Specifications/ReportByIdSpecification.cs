using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ReportByIdSpecification : BaseSpecification<Report>
{
  public ReportByIdSpecification(Guid reportId)
    : base(report => report.Id == reportId && !report.IsDeleted)
  {
    AddInclude(report => report.Parameters);
    ApplyAsNoTracking();
  }
}
