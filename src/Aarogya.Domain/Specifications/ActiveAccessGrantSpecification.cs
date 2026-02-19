using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Specifications;

public sealed class ActiveAccessGrantSpecification : BaseSpecification<AccessGrant>
{
  public ActiveAccessGrantSpecification(Guid patientId, Guid grantedToUserId, DateTimeOffset nowUtc)
    : base(grant =>
      grant.PatientId == patientId
      && grant.GrantedToUserId == grantedToUserId
      && grant.Status == AccessGrantStatus.Active
      && grant.StartsAt <= nowUtc
      && (grant.ExpiresAt == null || grant.ExpiresAt > nowUtc))
  {
    ApplyAsNoTracking();
  }
}
