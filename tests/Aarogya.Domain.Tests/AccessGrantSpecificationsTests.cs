using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests;

public sealed class AccessGrantSpecificationsTests
{
  [Fact]
  public void ActiveAccessGrantSpecification_ShouldMatchOnlyActiveWindowedGrantAsync()
  {
    var now = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
    var patientId = Guid.NewGuid();
    var doctorId = Guid.NewGuid();

    var specification = new ActiveAccessGrantSpecification(patientId, doctorId, now);
    var predicate = specification.Criteria!.Compile();

    var active = CreateGrant(patientId, doctorId, AccessGrantStatus.Active, now.AddMinutes(-1), now.AddDays(1));
    var expired = CreateGrant(patientId, doctorId, AccessGrantStatus.Active, now.AddDays(-1), now.AddSeconds(-1));
    var future = CreateGrant(patientId, doctorId, AccessGrantStatus.Active, now.AddMinutes(1), now.AddDays(1));
    var revoked = CreateGrant(patientId, doctorId, AccessGrantStatus.Revoked, now.AddDays(-1), now.AddDays(1));

    predicate(active).Should().BeTrue();
    predicate(expired).Should().BeFalse();
    predicate(future).Should().BeFalse();
    predicate(revoked).Should().BeFalse();
  }

  [Fact]
  public void ActiveAccessGrantsForDoctorSpecification_ShouldRequireReadScopeAsync()
  {
    var now = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
    var doctorId = Guid.NewGuid();

    var specification = new ActiveAccessGrantsForDoctorSpecification(doctorId, now);
    var predicate = specification.Criteria!.Compile();

    var readable = CreateGrant(Guid.NewGuid(), doctorId, AccessGrantStatus.Active, now.AddDays(-1), now.AddDays(1));
    readable.Scope = new AccessGrantScope
    {
      CanReadReports = true,
      CanDownloadReports = true
    };

    var notReadable = CreateGrant(Guid.NewGuid(), doctorId, AccessGrantStatus.Active, now.AddDays(-1), now.AddDays(1));
    notReadable.Scope = new AccessGrantScope
    {
      CanReadReports = false,
      CanDownloadReports = true
    };

    predicate(readable).Should().BeTrue();
    predicate(notReadable).Should().BeFalse();
  }

  [Fact]
  public void EmergencyContactByIdForUserSpecification_ShouldMatchExactOwnerAndContactAsync()
  {
    var userId = Guid.NewGuid();
    var contactId = Guid.NewGuid();
    var specification = new EmergencyContactByIdForUserSpecification(userId, contactId);
    var predicate = specification.Criteria!.Compile();

    var ownedContact = new EmergencyContact { Id = contactId, UserId = userId };
    var otherContact = new EmergencyContact { Id = Guid.NewGuid(), UserId = userId };
    var otherUserContact = new EmergencyContact { Id = contactId, UserId = Guid.NewGuid() };

    predicate(ownedContact).Should().BeTrue();
    predicate(otherContact).Should().BeFalse();
    predicate(otherUserContact).Should().BeFalse();
  }

  private static AccessGrant CreateGrant(
    Guid patientId,
    Guid doctorId,
    AccessGrantStatus status,
    DateTimeOffset startsAt,
    DateTimeOffset? expiresAt)
  {
    return new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = patientId,
      GrantedToUserId = doctorId,
      GrantedByUserId = patientId,
      Status = status,
      StartsAt = startsAt,
      ExpiresAt = expiresAt,
      Scope = new AccessGrantScope
      {
        CanReadReports = true,
        CanDownloadReports = true
      }
    };
  }
}
