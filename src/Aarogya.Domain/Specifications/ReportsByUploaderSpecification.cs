using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ReportsByUploaderSpecification : BaseSpecification<Report>
{
  public ReportsByUploaderSpecification(Guid uploaderId)
    : base(report => report.UploadedByUserId == uploaderId)
  {
    AddInclude(report => report.Parameters);
    ApplyOrderByDescending(report => report.UploadedAt);
    ApplyAsNoTracking();
  }
}
