using Aarogya.Api.Caching;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests.Caching;

public sealed class EntityCacheKeysTests
{
  [Fact]
  public void UserProfile_Should_ReturnDeterministicKey()
  {
    var key1 = EntityCacheKeys.UserProfile("user-sub-1");
    var key2 = EntityCacheKeys.UserProfile("user-sub-1");

    key1.Should().Be(key2);
  }

  [Fact]
  public void UserProfile_Should_ReturnDifferentKeys_ForDifferentInputs()
  {
    var key1 = EntityCacheKeys.UserProfile("user-sub-1");
    var key2 = EntityCacheKeys.UserProfile("user-sub-2");

    key1.Should().NotBe(key2);
  }

  [Fact]
  public void UserProfile_Should_TrimWhitespace()
  {
    var key1 = EntityCacheKeys.UserProfile("  user-sub-1  ");
    var key2 = EntityCacheKeys.UserProfile("user-sub-1");

    key1.Should().Be(key2);
  }

  [Fact]
  public void AccessGrantListForPatient_Should_ReturnDeterministicKey()
  {
    var key1 = EntityCacheKeys.AccessGrantListForPatient("patient-1", "v1");
    var key2 = EntityCacheKeys.AccessGrantListForPatient("patient-1", "v1");

    key1.Should().Be(key2);
  }

  [Fact]
  public void AccessGrantListForPatient_Should_DifferByVersion()
  {
    var key1 = EntityCacheKeys.AccessGrantListForPatient("patient-1", "v1");
    var key2 = EntityCacheKeys.AccessGrantListForPatient("patient-1", "v2");

    key1.Should().NotBe(key2);
  }

  [Fact]
  public void AccessGrantListForDoctor_Should_ReturnDeterministicKey()
  {
    var key1 = EntityCacheKeys.AccessGrantListForDoctor("doctor-1", "v1");
    var key2 = EntityCacheKeys.AccessGrantListForDoctor("doctor-1", "v1");

    key1.Should().Be(key2);
  }

  [Fact]
  public void ReportListing_Should_ReturnDeterministicKey()
  {
    var key1 = EntityCacheKeys.ReportListing("user-1", "fp-abc", "v1");
    var key2 = EntityCacheKeys.ReportListing("user-1", "fp-abc", "v1");

    key1.Should().Be(key2);
  }

  [Fact]
  public void ReportListing_Should_DifferByFingerprint()
  {
    var key1 = EntityCacheKeys.ReportListing("user-1", "fp-abc", "v1");
    var key2 = EntityCacheKeys.ReportListing("user-1", "fp-xyz", "v1");

    key1.Should().NotBe(key2);
  }

  [Fact]
  public void ReportListing_Should_TrimWhitespaceInAllInputs()
  {
    var key1 = EntityCacheKeys.ReportListing("  user-1  ", "  fp-abc  ", "v1");
    var key2 = EntityCacheKeys.ReportListing("user-1", "fp-abc", "v1");

    key1.Should().Be(key2);
  }
}
