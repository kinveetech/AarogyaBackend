using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Specifications;

public sealed class EmergencyAccessGrantSpecificationsTests
{
  [Fact]
  public void EmergencyAccessGrantsExpiredSpecification_ShouldMatchActiveExpiredEmergencyGrants()
  {
    var now = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);

    var specification = new EmergencyAccessGrantsExpiredSpecification(now);
    var predicate = specification.Criteria!.Compile();

    var expired = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(-5), "emergency:medical");
    var notExpired = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(5), "emergency:medical");
    var revoked = CreateGrant(AccessGrantStatus.Revoked, now.AddMinutes(-5), "emergency:medical");
    var notEmergency = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(-5), "standard:access");
    var nullReason = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(-5), null);
    var noExpiry = CreateGrantNoExpiry(AccessGrantStatus.Active, "emergency:medical");

    predicate(expired).Should().BeTrue();
    predicate(notExpired).Should().BeFalse();
    predicate(revoked).Should().BeFalse();
    predicate(notEmergency).Should().BeFalse();
    predicate(nullReason).Should().BeFalse();
    predicate(noExpiry).Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessGrantsExpiredSpecification_ShouldHaveTwoIncludesAndOrderBy()
  {
    var specification = new EmergencyAccessGrantsExpiredSpecification(DateTimeOffset.UtcNow);

    specification.Includes.Count.Should().Be(2);
    specification.OrderBy.Should().NotBeNull();
    specification.OrderByDescending.Should().BeNull();
    specification.AsNoTracking.Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessGrantsDueForPreExpiryNotificationSpecification_ShouldMatchGrantsDueForNotification()
  {
    var now = new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);
    var leadMinutes = 15;

    var specification =
      new EmergencyAccessGrantsDueForPreExpiryNotificationSpecification(now, leadMinutes);
    var predicate = specification.Criteria!.Compile();

    var dueForNotification = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(10), "emergency:medical");
    var tooFarAway = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(30), "emergency:medical");
    var alreadyExpired = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(-1), "emergency:medical");
    var alreadyNotified = CreateGrant(
      AccessGrantStatus.Active, now.AddMinutes(10), "emergency:medical|preexpiry_notified:true");
    var notEmergency = CreateGrant(AccessGrantStatus.Active, now.AddMinutes(10), "standard:access");
    var revoked = CreateGrant(AccessGrantStatus.Revoked, now.AddMinutes(10), "emergency:medical");

    predicate(dueForNotification).Should().BeTrue();
    predicate(tooFarAway).Should().BeFalse();
    predicate(alreadyExpired).Should().BeFalse();
    predicate(alreadyNotified).Should().BeFalse();
    predicate(notEmergency).Should().BeFalse();
    predicate(revoked).Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessGrantsDueForPreExpiryNotificationSpecification_ShouldHaveTwoIncludesAndOrderBy()
  {
    var specification =
      new EmergencyAccessGrantsDueForPreExpiryNotificationSpecification(DateTimeOffset.UtcNow, 15);

    specification.Includes.Count.Should().Be(2);
    specification.OrderBy.Should().NotBeNull();
    specification.OrderByDescending.Should().BeNull();
  }

  private static AccessGrant CreateGrant(
    AccessGrantStatus status,
    DateTimeOffset expiresAt,
    string? grantReason)
  {
    return new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = Guid.NewGuid(),
      GrantedToUserId = Guid.NewGuid(),
      GrantedByUserId = Guid.NewGuid(),
      Status = status,
      StartsAt = expiresAt.AddHours(-1),
      ExpiresAt = expiresAt,
      GrantReason = grantReason,
      Scope = new AccessGrantScope { CanReadReports = true, CanDownloadReports = true }
    };
  }

  private static AccessGrant CreateGrantNoExpiry(AccessGrantStatus status, string? grantReason)
  {
    return new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = Guid.NewGuid(),
      GrantedToUserId = Guid.NewGuid(),
      GrantedByUserId = Guid.NewGuid(),
      Status = status,
      StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
      ExpiresAt = null,
      GrantReason = grantReason,
      Scope = new AccessGrantScope { CanReadReports = true, CanDownloadReports = true }
    };
  }
}
