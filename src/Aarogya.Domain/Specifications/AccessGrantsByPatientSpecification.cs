using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class AccessGrantsByPatientSpecification : BaseSpecification<AccessGrant>
{
  public AccessGrantsByPatientSpecification(Guid patientId)
    : base(grant => grant.PatientId == patientId)
  {
    AddInclude(grant => grant.GrantedToUser);
    ApplyOrderByDescending(grant => grant.CreatedAt);
    ApplyAsNoTracking();
  }
}
