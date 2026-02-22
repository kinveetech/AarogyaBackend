using Aarogya.Domain.Entities;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Domain.Tests.Specifications;

public sealed class UserSpecificationsTests
{
  [Fact]
  public void UserByExternalAuthIdSpecification_ShouldMatchByExternalAuthId()
  {
    var specification = new UserByExternalAuthIdSpecification("auth-123");
    var predicate = specification.Criteria!.Compile();

    var match = new User { ExternalAuthId = "auth-123" };
    var noMatch = new User { ExternalAuthId = "auth-456" };
    var nullId = new User { ExternalAuthId = null };

    predicate(match).Should().BeTrue();
    predicate(noMatch).Should().BeFalse();
    predicate(nullId).Should().BeFalse();
  }

  [Fact]
  public void UserByExternalAuthIdSpecification_ShouldApplyAsNoTracking()
  {
    var specification = new UserByExternalAuthIdSpecification("auth-123");

    specification.AsNoTracking.Should().BeTrue();
  }
}
