using Aarogya.Domain.Entities;

namespace Aarogya.Domain.Specifications;

public sealed class AccessGrantsByUserSpecification : BaseSpecification<AccessGrant>
{
  public AccessGrantsByUserSpecification(Guid userId)
    : base(grant =>
      grant.PatientId == userId
      || grant.GrantedToUserId == userId
      || grant.GrantedByUserId == userId)
  {
    ApplyOrderByDescending(grant => grant.CreatedAt);
  }
}
