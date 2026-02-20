using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Specifications;

public sealed class ActiveAccessGrantsForDoctorSpecification : BaseSpecification<AccessGrant>
{
  public ActiveAccessGrantsForDoctorSpecification(Guid doctorId, DateTimeOffset asOf)
    : base(grant => grant.GrantedToUserId == doctorId
      && grant.Status == AccessGrantStatus.Active
      && grant.StartsAt <= asOf
      && (grant.ExpiresAt == null || grant.ExpiresAt > asOf)
      && grant.Scope.CanReadReports)
  {
    ApplyAsNoTracking();
  }
}
