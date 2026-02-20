using System.Collections.ObjectModel;
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

  [Fact]
  public void Encrypt_ShouldStampActiveKeyId()
  {
    var service = CreateService();

    var encrypted = service.Encrypt("alice@example.com");

    service.GetEncryptionKeyId(encrypted).Should().Be("local-current");
  }

  [Fact]
  public void Decrypt_ShouldSupportLegacyLocalKeyRing()
  {
    var legacyService = CreateService("legacy-local-key", "local-legacy");
    var encryptedWithLegacy = legacyService.Encrypt("alice@example.com");

    var service = CreateService(
      activeLocalKey: "current-local-key",
      activeKeyId: "local-current",
      legacy: new Collection<LegacyEncryptionKeyOptions>
      {
        new() { KeyId = "local-legacy", Secret = "legacy-local-key" }
      });

    var decrypted = service.Decrypt(encryptedWithLegacy);

    decrypted.Should().Be("alice@example.com");
  }

  private static PiiFieldEncryptionService CreateService(
    string activeLocalKey = "test-local-data-key",
    string activeKeyId = "local-current",
    Collection<LegacyEncryptionKeyOptions>? legacy = null)
  {
    var options = Options.Create(new EncryptionOptions
    {
      UseAwsKms = false,
      ActiveKeyId = activeKeyId,
      LocalDataKey = activeLocalKey,
      LegacyLocalDataKeys = legacy ?? [],
      BlindIndexKey = "blind-index-key"
    });

    return new PiiFieldEncryptionService(options);
  }
}
