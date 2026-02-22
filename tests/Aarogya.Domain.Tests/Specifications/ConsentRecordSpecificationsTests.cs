using Aarogya.Domain.Entities;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Specifications;

public sealed class ConsentRecordSpecificationsTests
{
  [Fact]
  public void ConsentRecordsByUserSpecification_ShouldMatchByUserId()
  {
    var userId = Guid.NewGuid();

    var specification = new ConsentRecordsByUserSpecification(userId);
    var predicate = specification.Criteria!.Compile();

    var match = new ConsentRecord
    {
      Id = Guid.NewGuid(),
      UserId = userId,
      OccurredAt = DateTimeOffset.UtcNow
    };
    var noMatch = new ConsentRecord
    {
      Id = Guid.NewGuid(),
      UserId = Guid.NewGuid(),
      OccurredAt = DateTimeOffset.UtcNow
    };

    predicate(match).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
  }

  [Fact]
  public void ConsentRecordsByUserSpecification_ShouldHaveOrderByDescendingAndNoAsNoTracking()
  {
    var specification = new ConsentRecordsByUserSpecification(Guid.NewGuid());

    specification.OrderByDescending.Should().NotBeNull();
    specification.AsNoTracking.Should().BeFalse();
  }
}
