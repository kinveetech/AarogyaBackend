using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Specifications;
using Aarogya.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Specifications;

public sealed class AccessGrantSpecificationsExtendedTests
{
  [Fact]
  public void ActiveAccessGrantsByDoctorSpecification_ShouldMatchActiveGrantsForDoctor()
  {
    var now = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);
    var doctorId = Guid.NewGuid();

    var specification = new ActiveAccessGrantsByDoctorSpecification(doctorId, now);
    var predicate = specification.Criteria!.Compile();

    var active = CreateGrant(Guid.NewGuid(), doctorId, Guid.NewGuid(), AccessGrantStatus.Active,
      now.AddDays(-1), now.AddDays(1));
    var expired = CreateGrant(Guid.NewGuid(), doctorId, Guid.NewGuid(), AccessGrantStatus.Active,
      now.AddDays(-2), now.AddSeconds(-1));
    var future = CreateGrant(Guid.NewGuid(), doctorId, Guid.NewGuid(), AccessGrantStatus.Active,
      now.AddMinutes(1), now.AddDays(1));
    var revoked = CreateGrant(Guid.NewGuid(), doctorId, Guid.NewGuid(), AccessGrantStatus.Revoked,
      now.AddDays(-1), now.AddDays(1));
    var otherDoctor = CreateGrant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      now.AddDays(-1), now.AddDays(1));

    predicate(active).Should().BeTrue();
    predicate(expired).Should().BeFalse();
    predicate(future).Should().BeFalse();
    predicate(revoked).Should().BeFalse();
    predicate(otherDoctor).Should().BeFalse();
  }

  [Fact]
  public void ActiveAccessGrantsByDoctorSpecification_ShouldHaveTwoIncludesOrderByDescendingAndAsNoTracking()
  {
    var specification = new ActiveAccessGrantsByDoctorSpecification(Guid.NewGuid(), DateTimeOffset.UtcNow);

    specification.Includes.Count.Should().Be(2);
    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeTrue();
  }

  [Fact]
  public void AccessGrantsByPatientSpecification_ShouldMatchGrantsForPatient()
  {
    var patientId = Guid.NewGuid();

    var specification = new AccessGrantsByPatientSpecification(patientId);
    var predicate = specification.Criteria!.Compile();

    var match = CreateGrant(patientId, Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
    var noMatch = CreateGrant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

    predicate(match).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
  }

  [Fact]
  public void AccessGrantsByPatientSpecification_ShouldHaveOneIncludeOrderByDescendingAndAsNoTracking()
  {
    var specification = new AccessGrantsByPatientSpecification(Guid.NewGuid());

    specification.Includes.Count.Should().Be(1);
    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeTrue();
  }

  [Fact]
  public void AccessGrantByIdForPatientSpecification_ShouldMatchExactPatientAndGrantId()
  {
    var patientId = Guid.NewGuid();
    var grantId = Guid.NewGuid();

    var specification = new AccessGrantByIdForPatientSpecification(patientId, grantId);
    var predicate = specification.Criteria!.Compile();

    var match = CreateGrant(patientId, Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
    match.Id = grantId;

    var wrongPatient = CreateGrant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
    wrongPatient.Id = grantId;

    var wrongGrant = CreateGrant(patientId, Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

    predicate(match).Should().BeTrue();
    predicate(wrongPatient).Should().BeFalse();
    predicate(wrongGrant).Should().BeFalse();
  }

  [Fact]
  public void AccessGrantByIdForPatientSpecification_ShouldNotApplyAsNoTracking()
  {
    var specification = new AccessGrantByIdForPatientSpecification(Guid.NewGuid(), Guid.NewGuid());

    specification.AsNoTracking.Should().BeFalse();
  }

  [Fact]
  public void AccessGrantsByUserSpecification_ShouldMatchGrantsWhereUserIsPatientOrGrantedToOrGrantedBy()
  {
    var userId = Guid.NewGuid();

    var specification = new AccessGrantsByUserSpecification(userId);
    var predicate = specification.Criteria!.Compile();

    var asPatient = CreateGrant(userId, Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
    var asGrantedTo = CreateGrant(Guid.NewGuid(), userId, Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
    var asGrantedBy = CreateGrant(Guid.NewGuid(), Guid.NewGuid(), userId, AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
    var noMatch = CreateGrant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AccessGrantStatus.Active,
      DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

    predicate(asPatient).Should().BeTrue();
    predicate(asGrantedTo).Should().BeTrue();
    predicate(asGrantedBy).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
  }

  [Fact]
  public void AccessGrantsByUserSpecification_ShouldHaveOrderByDescendingAndNoAsNoTracking()
  {
    var specification = new AccessGrantsByUserSpecification(Guid.NewGuid());

    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeFalse();
  }

  private static AccessGrant CreateGrant(
    Guid patientId,
    Guid grantedToUserId,
    Guid grantedByUserId,
    AccessGrantStatus status,
    DateTimeOffset startsAt,
    DateTimeOffset? expiresAt)
  {
    return new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = patientId,
      GrantedToUserId = grantedToUserId,
      GrantedByUserId = grantedByUserId,
      Status = status,
      StartsAt = startsAt,
      ExpiresAt = expiresAt,
      Scope = new AccessGrantScope { CanReadReports = true, CanDownloadReports = true }
    };
  }
}
