using System.Security.Cryptography;
using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Infrastructure.Security;

public sealed class PiiFieldEncryptionService : IPiiFieldEncryptionService
{
  private const int NonceLength = 12;
  private const int TagLength = 16;
  private const byte PayloadVersion = 1;

  private readonly EncryptionOptions _options;
  private readonly IAmazonKeyManagementService? _kmsClient;
  private readonly Lazy<byte[]> _dataKey;

  public PiiFieldEncryptionService(
    IOptions<EncryptionOptions> options,
    IAmazonKeyManagementService? kmsClient = null)
  {
    ArgumentNullException.ThrowIfNull(options);

    _options = options.Value;
    _kmsClient = kmsClient;
    _dataKey = new Lazy<byte[]>(ResolveDataKey, isThreadSafe: true);
  }

  public byte[]? Encrypt(string? plaintext)
  {
    if (plaintext is null)
    {
      return null;
    }

    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
    var nonce = RandomNumberGenerator.GetBytes(NonceLength);
    var ciphertext = new byte[plaintextBytes.Length];
    var tag = new byte[TagLength];

    using (var aes = new AesGcm(_dataKey.Value, TagLength))
    {
      aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
    }

    var payload = new byte[1 + NonceLength + TagLength + ciphertext.Length];
    payload[0] = PayloadVersion;
    Buffer.BlockCopy(nonce, 0, payload, 1, NonceLength);
    Buffer.BlockCopy(tag, 0, payload, 1 + NonceLength, TagLength);
    Buffer.BlockCopy(ciphertext, 0, payload, 1 + NonceLength + TagLength, ciphertext.Length);

    return payload;
  }

  public string? Decrypt(byte[]? ciphertext)
  {
    if (ciphertext is null)
    {
      return null;
    }

    if (ciphertext.Length < 1 + NonceLength + TagLength)
    {
      throw new CryptographicException("Invalid encrypted payload.");
    }

    if (ciphertext[0] != PayloadVersion)
    {
      throw new CryptographicException($"Unsupported encrypted payload version '{ciphertext[0]}'.");
    }

    var nonce = ciphertext.AsSpan(1, NonceLength).ToArray();
    var tag = ciphertext.AsSpan(1 + NonceLength, TagLength).ToArray();
    var encryptedData = ciphertext.AsSpan(1 + NonceLength + TagLength).ToArray();
    var plaintext = new byte[encryptedData.Length];

    using (var aes = new AesGcm(_dataKey.Value, TagLength))
    {
      aes.Decrypt(nonce, encryptedData, tag, plaintext);
    }

    return Encoding.UTF8.GetString(plaintext);
  }

  private byte[] ResolveDataKey()
  {
    if (_options.UseAwsKms)
    {
      if (_kmsClient is null)
      {
        throw new InvalidOperationException("AWS KMS is enabled but IAmazonKeyManagementService is not registered.");
      }

      if (string.IsNullOrWhiteSpace(_options.KmsKeyId))
      {
        throw new InvalidOperationException("Encryption:KmsKeyId must be configured when AWS KMS is enabled.");
      }

      var request = new GenerateDataKeyRequest
      {
        KeyId = _options.KmsKeyId,
        KeySpec = DataKeySpec.AES_256
      };

      var response = _kmsClient.GenerateDataKeyAsync(request).GetAwaiter().GetResult();
      if (response.Plaintext is null)
      {
        throw new InvalidOperationException("AWS KMS did not return plaintext data key material.");
      }

      return response.Plaintext.ToArray();
    }

    if (!string.IsNullOrWhiteSpace(_options.LocalDataKey)
      && _options.LocalDataKey != "SET_VIA_USER_SECRETS_OR_ENV_VAR")
    {
      return SHA256.HashData(Encoding.UTF8.GetBytes(_options.LocalDataKey));
    }

    return SHA256.HashData(Encoding.UTF8.GetBytes("aarogya-dev-local-data-key"));
  }
}
