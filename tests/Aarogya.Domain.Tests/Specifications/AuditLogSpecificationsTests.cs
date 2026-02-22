using Aarogya.Domain.Entities;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Specifications;

public sealed class AuditLogSpecificationsTests
{
  [Fact]
  public void AuditLogsByActorSpecification_ShouldMatchByActorUserId()
  {
    var actorId = Guid.NewGuid();

    var specification = new AuditLogsByActorSpecification(actorId);
    var predicate = specification.Criteria!.Compile();

    var match = CreateAuditLog(actorId, "user.login", 200);
    var noMatch = CreateAuditLog(Guid.NewGuid(), "user.login", 200);

    predicate(match).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
  }

  [Fact]
  public void AuditLogsByActorSpecification_ShouldHaveOrderByDescendingAndAsNoTracking()
  {
    var specification = new AuditLogsByActorSpecification(Guid.NewGuid());

    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeTrue();
  }

  [Fact]
  public void RecentAuditLogsSpecification_ShouldMatchLogsInRangeWithActorAndSuccessStatus()
  {
    var from = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    var to = new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero);

    var specification = new RecentAuditLogsSpecification(from, to);
    var predicate = specification.Criteria!.Compile();

    var match = CreateAuditLog(Guid.NewGuid(), "user.login", 200);
    match.OccurredAt = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);

    var beforeRange = CreateAuditLog(Guid.NewGuid(), "user.login", 200);
    beforeRange.OccurredAt = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

    var afterRange = CreateAuditLog(Guid.NewGuid(), "user.login", 200);
    afterRange.OccurredAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    var noActor = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = null,
      Action = "system.task",
      ResultStatus = 200,
      OccurredAt = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero)
    };

    var errorStatus = CreateAuditLog(Guid.NewGuid(), "user.login", 500);
    errorStatus.OccurredAt = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero);

    var nullStatus = new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = Guid.NewGuid(),
      Action = "user.login",
      ResultStatus = null,
      OccurredAt = new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero)
    };

    predicate(match).Should().BeTrue();
    predicate(beforeRange).Should().BeFalse();
    predicate(afterRange).Should().BeFalse();
    predicate(noActor).Should().BeFalse();
    predicate(errorStatus).Should().BeFalse();
    predicate(nullStatus).Should().BeFalse();
  }

  [Fact]
  public void RecentAuditLogsSpecification_ShouldAcceptAllSuccessStatusCodes()
  {
    var from = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    var to = new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero);

    var specification = new RecentAuditLogsSpecification(from, to);
    var predicate = specification.Criteria!.Compile();

    var status200 = CreateAuditLog(Guid.NewGuid(), "action", 200);
    status200.OccurredAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    var status201 = CreateAuditLog(Guid.NewGuid(), "action", 201);
    status201.OccurredAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    var status299 = CreateAuditLog(Guid.NewGuid(), "action", 299);
    status299.OccurredAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    var status300 = CreateAuditLog(Guid.NewGuid(), "action", 300);
    status300.OccurredAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    predicate(status200).Should().BeTrue();
    predicate(status201).Should().BeTrue();
    predicate(status299).Should().BeTrue();
    predicate(status300).Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessAuditLogsSpecification_ShouldMatchEmergencyAccessActions()
  {
    var specification = new EmergencyAccessAuditLogsSpecification(null, null);
    var predicate = specification.Criteria!.Compile();

    var match = CreateAuditLog(Guid.NewGuid(), "emergency_access.granted", 200);
    var noMatch = CreateAuditLog(Guid.NewGuid(), "user.login", 200);

    predicate(match).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessAuditLogsSpecification_ShouldFilterByFromWhenProvided()
  {
    var from = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero);

    var specification = new EmergencyAccessAuditLogsSpecification(from, null);
    var predicate = specification.Criteria!.Compile();

    var inRange = CreateAuditLog(Guid.NewGuid(), "emergency_access.revoked", 200);
    inRange.OccurredAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    var beforeRange = CreateAuditLog(Guid.NewGuid(), "emergency_access.revoked", 200);
    beforeRange.OccurredAt = new DateTimeOffset(2026, 2, 9, 23, 59, 59, TimeSpan.Zero);

    predicate(inRange).Should().BeTrue();
    predicate(beforeRange).Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessAuditLogsSpecification_ShouldFilterByToWhenProvided()
  {
    var to = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);

    var specification = new EmergencyAccessAuditLogsSpecification(null, to);
    var predicate = specification.Criteria!.Compile();

    var inRange = CreateAuditLog(Guid.NewGuid(), "emergency_access.granted", 200);
    inRange.OccurredAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    var afterRange = CreateAuditLog(Guid.NewGuid(), "emergency_access.granted", 200);
    afterRange.OccurredAt = new DateTimeOffset(2026, 2, 20, 0, 0, 1, TimeSpan.Zero);

    predicate(inRange).Should().BeTrue();
    predicate(afterRange).Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessAuditLogsSpecification_ShouldFilterByBothFromAndTo()
  {
    var from = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero);
    var to = new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero);

    var specification = new EmergencyAccessAuditLogsSpecification(from, to);
    var predicate = specification.Criteria!.Compile();

    var inRange = CreateAuditLog(Guid.NewGuid(), "emergency_access.granted", 200);
    inRange.OccurredAt = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    var beforeRange = CreateAuditLog(Guid.NewGuid(), "emergency_access.granted", 200);
    beforeRange.OccurredAt = new DateTimeOffset(2026, 2, 9, 0, 0, 0, TimeSpan.Zero);

    var afterRange = CreateAuditLog(Guid.NewGuid(), "emergency_access.granted", 200);
    afterRange.OccurredAt = new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);

    predicate(inRange).Should().BeTrue();
    predicate(beforeRange).Should().BeFalse();
    predicate(afterRange).Should().BeFalse();
  }

  [Fact]
  public void EmergencyAccessAuditLogsSpecification_ShouldHaveOrderByDescendingAndAsNoTracking()
  {
    var specification = new EmergencyAccessAuditLogsSpecification(null, null);

    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeTrue();
  }

  private static AuditLog CreateAuditLog(Guid actorUserId, string action, int resultStatus)
  {
    return new AuditLog
    {
      Id = Guid.NewGuid(),
      ActorUserId = actorUserId,
      Action = action,
      ResultStatus = resultStatus,
      OccurredAt = DateTimeOffset.UtcNow
    };
  }
}
