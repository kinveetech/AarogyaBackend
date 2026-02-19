using Aarogya.Infrastructure.Aadhaar;
using FluentAssertions;
using Xunit;

namespace Aarogya.Infrastructure.Tests;

public sealed class AadhaarHashingTests
{
  [Fact]
  public void Normalize_ShouldStripNonDigits()
  {
    var normalized = AadhaarHashing.Normalize("1234 5678 9012");

    normalized.Should().Be("123456789012");
  }

  [Fact]
  public void Normalize_ShouldThrow_ForInvalidLength()
  {
    var action = () => AadhaarHashing.Normalize("12345");

    action.Should().Throw<ArgumentException>();
  }

  [Fact]
  public void ComputeSha256_ShouldReturnDeterministicValue()
  {
    var first = AadhaarHashing.ComputeSha256("123456789012");
    var second = AadhaarHashing.ComputeSha256("123456789012");

    first.Should().Equal(second);
  }
}
