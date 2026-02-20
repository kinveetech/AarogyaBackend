using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Specifications;

public sealed class EmergencyAccessGrantsExpiredSpecification : BaseSpecification<AccessGrant>
{
  private const string EmergencyPrefix = "emergency:";

  public EmergencyAccessGrantsExpiredSpecification(DateTimeOffset nowUtc)
    : base(grant =>
      grant.Status == AccessGrantStatus.Active
      && grant.ExpiresAt.HasValue
      && grant.ExpiresAt.Value <= nowUtc
      && grant.GrantReason != null
      && grant.GrantReason.StartsWith(EmergencyPrefix))
  {
    AddInclude(grant => grant.Patient);
    AddInclude(grant => grant.GrantedToUser);
    ApplyOrderBy(grant => grant.ExpiresAt!);
  }
}
