using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class AccessGrantByIdForPatientSpecification : BaseSpecification<AccessGrant>
{
  public AccessGrantByIdForPatientSpecification(Guid patientId, Guid grantId)
    : base(grant => grant.PatientId == patientId && grant.Id == grantId)
  {
  }
}
