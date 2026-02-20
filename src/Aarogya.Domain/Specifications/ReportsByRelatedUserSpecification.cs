using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ReportsByRelatedUserSpecification : BaseSpecification<Report>
{
  public ReportsByRelatedUserSpecification(Guid userId, bool includeDeleted = false)
    : base(report =>
      (report.PatientId == userId
       || report.UploadedByUserId == userId
       || report.DoctorId == userId)
      && (includeDeleted || !report.IsDeleted))
  {
    AddInclude(report => report.Parameters);
    ApplyOrderByDescending(report => report.UploadedAt);
  }
}
