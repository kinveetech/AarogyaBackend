using Aarogya.Infrastructure.Security;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aarogya.Infrastructure.Persistence.Converters;

internal sealed class EncryptedRequiredStringToBytesConverter(IPiiFieldEncryptionService encryptionService)
  : ValueConverter<string, byte[]>(
      value => encryptionService.Encrypt(value) ?? Array.Empty<byte>(),
      value => encryptionService.Decrypt(value) ?? string.Empty)
{
}

internal sealed class EncryptedNullableStringToBytesConverter(IPiiFieldEncryptionService encryptionService)
  : ValueConverter<string?, byte[]?>(
      value => encryptionService.Encrypt(value),
      value => encryptionService.Decrypt(value))
{
}
