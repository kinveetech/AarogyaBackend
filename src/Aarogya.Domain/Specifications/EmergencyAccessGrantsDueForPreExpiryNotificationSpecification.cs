using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;

namespace Aarogya.Domain.Specifications;

public sealed class EmergencyAccessGrantsDueForPreExpiryNotificationSpecification : BaseSpecification<AccessGrant>
{
  private const string EmergencyPrefix = "emergency:";
  private const string NotifiedMarker = "|preexpiry_notified:";

  public EmergencyAccessGrantsDueForPreExpiryNotificationSpecification(DateTimeOffset nowUtc, int leadMinutes)
    : base(grant =>
      grant.Status == AccessGrantStatus.Active
      && grant.ExpiresAt.HasValue
      && grant.ExpiresAt.Value > nowUtc
      && grant.ExpiresAt.Value <= nowUtc.AddMinutes(leadMinutes)
      && grant.GrantReason != null
      && grant.GrantReason.StartsWith(EmergencyPrefix)
      && !grant.GrantReason.Contains(NotifiedMarker))
  {
    AddInclude(grant => grant.Patient);
    AddInclude(grant => grant.GrantedToUser);
    ApplyOrderBy(grant => grant.ExpiresAt!);
  }
}
