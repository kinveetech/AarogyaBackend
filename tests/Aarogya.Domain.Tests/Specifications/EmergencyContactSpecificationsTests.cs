using Aarogya.Domain.Entities;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Specifications;

public sealed class EmergencyContactSpecificationsTests
{
  [Fact]
  public void EmergencyContactsByUserSpecification_ShouldMatchByUserId()
  {
    var userId = Guid.NewGuid();

    var specification = new EmergencyContactsByUserSpecification(userId);
    var predicate = specification.Criteria!.Compile();

    var match = new EmergencyContact { Id = Guid.NewGuid(), UserId = userId, IsPrimary = true };
    var noMatch = new EmergencyContact { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), IsPrimary = false };

    predicate(match).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
  }

  [Fact]
  public void EmergencyContactsByUserSpecification_ShouldHaveOrderByDescendingAndAsNoTracking()
  {
    var specification = new EmergencyContactsByUserSpecification(Guid.NewGuid());

    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeTrue();
  }
}
