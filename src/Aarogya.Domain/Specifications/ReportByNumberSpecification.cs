using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ReportByNumberSpecification : BaseSpecification<Report>
{
  public ReportByNumberSpecification(string reportNumber)
    : base(report => report.ReportNumber == reportNumber)
  {
    AddInclude(report => report.Parameters);
    ApplyAsNoTracking();
  }
}
