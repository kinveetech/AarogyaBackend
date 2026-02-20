using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Specifications;

public sealed class ActiveAccessGrantsByDoctorSpecification : BaseSpecification<AccessGrant>
{
  public ActiveAccessGrantsByDoctorSpecification(Guid doctorId, DateTimeOffset nowUtc)
    : base(grant =>
      grant.GrantedToUserId == doctorId
      && grant.Status == AccessGrantStatus.Active
      && grant.StartsAt <= nowUtc
      && (grant.ExpiresAt == null || grant.ExpiresAt > nowUtc))
  {
    AddInclude(grant => grant.Patient);
    AddInclude(grant => grant.GrantedToUser);
    ApplyOrderByDescending(grant => grant.CreatedAt);
    ApplyAsNoTracking();
  }
}
