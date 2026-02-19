using Aarogya.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Infrastructure.Tests;

public sealed class BlindIndexServiceTests
{
  [Fact]
  public void Compute_ShouldReturnDeterministicHash_ForNormalizedInput()
  {
    var service = CreateService();

    var first = service.Compute("User@Example.com", "users.email");
    var second = service.Compute(" user@example.com ", "users.email");

    first.Should().Equal(second!);
  }

  [Fact]
  public void Compute_ShouldReturnDifferentHash_ForDifferentScopes()
  {
    var service = CreateService();

    var emailHash = service.Compute("user@example.com", "users.email");
    var phoneHash = service.Compute("user@example.com", "users.phone");

    emailHash.Should().NotEqual(phoneHash);
  }

  [Fact]
  public void Compute_ShouldReturnNull_ForMissingInput()
  {
    var service = CreateService();

    service.Compute(null, "users.email").Should().BeNull();
    service.Compute(" ", "users.email").Should().BeNull();
  }

  private static BlindIndexService CreateService()
  {
    var options = Options.Create(new EncryptionOptions
    {
      BlindIndexKey = "blind-index-key"
    });

    return new BlindIndexService(options);
  }
}
