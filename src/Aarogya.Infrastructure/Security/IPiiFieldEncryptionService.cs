namespace Aarogya.Infrastructure.Security;

public interface IPiiFieldEncryptionService
{
  public string ActiveKeyId { get; }

  public byte[]? Encrypt(string? plaintext);

  public string? Decrypt(byte[]? ciphertext);

  public string? GetEncryptionKeyId(byte[]? ciphertext);
}
