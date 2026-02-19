namespace Aarogya.Infrastructure.Security;

public interface IPiiFieldEncryptionService
{
  public byte[]? Encrypt(string? plaintext);

  public string? Decrypt(byte[]? ciphertext);
}
