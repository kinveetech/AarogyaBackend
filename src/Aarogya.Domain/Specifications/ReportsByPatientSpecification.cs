using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class ReportsByPatientSpecification : BaseSpecification<Report>
{
  public ReportsByPatientSpecification(Guid patientId)
    : base(report => report.PatientId == patientId)
  {
    AddInclude(report => report.Parameters);
    ApplyOrderByDescending(report => report.UploadedAt);
    ApplyAsNoTracking();
  }
}
