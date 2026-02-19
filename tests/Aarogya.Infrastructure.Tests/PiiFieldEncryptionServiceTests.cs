using Aarogya.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Infrastructure.Tests;

public sealed class PiiFieldEncryptionServiceTests
{
  [Fact]
  public void EncryptAndDecrypt_ShouldRoundTrip()
  {
    var service = CreateService();

    var encrypted = service.Encrypt("alice@example.com");
    var decrypted = service.Decrypt(encrypted);

    decrypted.Should().Be("alice@example.com");
  }

  [Fact]
  public void Encrypt_ShouldUseRandomNonce()
  {
    var service = CreateService();

    var first = service.Encrypt("alice@example.com");
    var second = service.Encrypt("alice@example.com");

    first.Should().NotEqual(second);
  }

  [Fact]
  public void EncryptAndDecrypt_ShouldHandleNullValues()
  {
    var service = CreateService();

    service.Encrypt(null).Should().BeNull();
    service.Decrypt(null).Should().BeNull();
  }

  private static PiiFieldEncryptionService CreateService()
  {
    var options = Options.Create(new EncryptionOptions
    {
      UseAwsKms = false,
      LocalDataKey = "test-local-data-key",
      BlindIndexKey = "blind-index-key"
    });

    return new PiiFieldEncryptionService(options);
  }
}
