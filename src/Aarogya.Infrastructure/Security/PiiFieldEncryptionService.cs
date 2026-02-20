using System.Buffers.Binary;
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
  private const byte LegacyPayloadVersion = 1;
  private const byte PayloadVersion = 2;

  private readonly EncryptionOptions _options;
  private readonly IAmazonKeyManagementService? _kmsClient;
  private readonly LocalKeyRing _localKeyRing;

  public PiiFieldEncryptionService(
    IOptions<EncryptionOptions> options,
    IAmazonKeyManagementService? kmsClient = null)
  {
    ArgumentNullException.ThrowIfNull(options);

    _options = options.Value;
    _kmsClient = kmsClient;
    _localKeyRing = BuildLocalKeyRing(_options);
    ActiveKeyId = ResolveActiveKeyId(_options);
  }

  public string ActiveKeyId { get; }

  public byte[]? Encrypt(string? plaintext)
  {
    if (plaintext is null)
    {
      return null;
    }

    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
    return _options.UseAwsKms
      ? EncryptWithKmsEnvelope(plaintextBytes)
      : EncryptWithLocalKey(plaintextBytes, ActiveKeyId);
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

    var version = ciphertext[0];
    return version switch
    {
      PayloadVersion => DecryptV2(ciphertext),
      LegacyPayloadVersion => DecryptLegacyV1(ciphertext),
      _ => throw new CryptographicException($"Unsupported encrypted payload version '{version}'.")
    };
  }

  public string? GetEncryptionKeyId(byte[]? ciphertext)
  {
    if (ciphertext is null || ciphertext.Length == 0)
    {
      return null;
    }

    if (ciphertext[0] == LegacyPayloadVersion)
    {
      return "legacy-v1";
    }

    if (ciphertext[0] != PayloadVersion)
    {
      return null;
    }

    TryParseV2Payload(ciphertext, out var keyId, out _, out _, out _, out _);
    return keyId;
  }

  private byte[] EncryptWithLocalKey(ReadOnlySpan<byte> plaintextBytes, string keyId)
  {
    var keyMaterial = ResolveLocalKeyById(keyId);
    var nonce = RandomNumberGenerator.GetBytes(NonceLength);
    var tag = new byte[TagLength];
    var encryptedData = new byte[plaintextBytes.Length];

    using (var aes = new AesGcm(keyMaterial, TagLength))
    {
      aes.Encrypt(nonce, plaintextBytes, encryptedData, tag);
    }

    return BuildV2Payload(keyId, wrappedKey: null, nonce, tag, encryptedData);
  }

  private byte[] EncryptWithKmsEnvelope(ReadOnlySpan<byte> plaintextBytes)
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
    if (response.Plaintext is null || response.CiphertextBlob is null)
    {
      throw new InvalidOperationException("AWS KMS did not return a usable data key.");
    }

    var dataKey = response.Plaintext.ToArray();
    var wrappedKey = response.CiphertextBlob.ToArray();
    var nonce = RandomNumberGenerator.GetBytes(NonceLength);
    var tag = new byte[TagLength];
    var encryptedData = new byte[plaintextBytes.Length];

    try
    {
      using (var aes = new AesGcm(dataKey, TagLength))
      {
        aes.Encrypt(nonce, plaintextBytes, encryptedData, tag);
      }
    }
    finally
    {
      CryptographicOperations.ZeroMemory(dataKey);
    }

    return BuildV2Payload(ActiveKeyId, wrappedKey, nonce, tag, encryptedData);
  }

  private string DecryptV2(byte[] ciphertext)
  {
    TryParseV2Payload(ciphertext, out var keyId, out var wrappedKey, out var nonce, out var tag, out var encryptedData);

    var keyMaterial = wrappedKey.Length > 0
      ? DecryptKmsWrappedDataKey(wrappedKey)
      : ResolveLocalKeyById(keyId);

    var plaintextBytes = new byte[encryptedData.Length];
    try
    {
      using (var aes = new AesGcm(keyMaterial, TagLength))
      {
        aes.Decrypt(nonce, encryptedData, tag, plaintextBytes);
      }
    }
    finally
    {
      if (wrappedKey.Length > 0)
      {
        CryptographicOperations.ZeroMemory(keyMaterial);
      }
    }

    return Encoding.UTF8.GetString(plaintextBytes);
  }

  private string DecryptLegacyV1(byte[] ciphertext)
  {
    var nonce = ciphertext.AsSpan(1, NonceLength).ToArray();
    var tag = ciphertext.AsSpan(1 + NonceLength, TagLength).ToArray();
    var encryptedData = ciphertext.AsSpan(1 + NonceLength + TagLength).ToArray();

    foreach (var candidateKey in _localKeyRing.V1FallbackKeys)
    {
      if (TryDecryptLegacyV1(candidateKey, nonce, tag, encryptedData, out var plaintext))
      {
        return plaintext;
      }
    }

    if (_options.UseAwsKms)
    {
      throw new CryptographicException("Legacy payload version 1 cannot be decrypted in AWS KMS mode.");
    }

    throw new CryptographicException("Failed to decrypt legacy payload with configured local key ring.");
  }

  private static bool TryDecryptLegacyV1(byte[] key, byte[] nonce, byte[] tag, byte[] encryptedData, out string plaintext)
  {
    var plaintextBytes = new byte[encryptedData.Length];
    try
    {
      using (var aes = new AesGcm(key, TagLength))
      {
        aes.Decrypt(nonce, encryptedData, tag, plaintextBytes);
      }

      plaintext = Encoding.UTF8.GetString(plaintextBytes);
      return true;
    }
    catch (CryptographicException)
    {
      plaintext = string.Empty;
      return false;
    }
  }

  private byte[] DecryptKmsWrappedDataKey(byte[] wrappedKey)
  {
    if (_kmsClient is null)
    {
      throw new InvalidOperationException("AWS KMS is enabled but IAmazonKeyManagementService is not registered.");
    }

    var request = new DecryptRequest
    {
      CiphertextBlob = new MemoryStream(wrappedKey)
    };

    var response = _kmsClient.DecryptAsync(request).GetAwaiter().GetResult();
    if (response.Plaintext is null)
    {
      throw new InvalidOperationException("AWS KMS did not return plaintext key material.");
    }

    return response.Plaintext.ToArray();
  }

  private byte[] ResolveLocalKeyById(string keyId)
  {
    if (!_localKeyRing.Keys.TryGetValue(keyId, out var material))
    {
      throw new CryptographicException($"Encryption key '{keyId}' is not configured.");
    }

    return material;
  }

  private static void TryParseV2Payload(
    byte[] payload,
    out string keyId,
    out byte[] wrappedKey,
    out byte[] nonce,
    out byte[] tag,
    out byte[] encryptedData)
  {
    if (payload.Length < 1 + 2 + 2 + NonceLength + TagLength)
    {
      throw new CryptographicException("Invalid encrypted payload.");
    }

    var cursor = 1;
    var keyIdLength = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(cursor, 2));
    cursor += 2;
    if (keyIdLength == 0 || cursor + keyIdLength > payload.Length)
    {
      throw new CryptographicException("Invalid encrypted payload key id.");
    }

    keyId = Encoding.UTF8.GetString(payload, cursor, keyIdLength);
    cursor += keyIdLength;

    if (cursor + 2 > payload.Length)
    {
      throw new CryptographicException("Invalid encrypted payload wrapped key header.");
    }

    var wrappedKeyLength = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(cursor, 2));
    cursor += 2;
    if (cursor + wrappedKeyLength + NonceLength + TagLength > payload.Length)
    {
      throw new CryptographicException("Invalid encrypted payload wrapped key.");
    }

    wrappedKey = payload.AsSpan(cursor, wrappedKeyLength).ToArray();
    cursor += wrappedKeyLength;
    nonce = payload.AsSpan(cursor, NonceLength).ToArray();
    cursor += NonceLength;
    tag = payload.AsSpan(cursor, TagLength).ToArray();
    cursor += TagLength;
    encryptedData = payload.AsSpan(cursor).ToArray();
  }

  private static byte[] BuildV2Payload(string keyId, byte[]? wrappedKey, byte[] nonce, byte[] tag, byte[] encryptedData)
  {
    var keyIdBytes = Encoding.UTF8.GetBytes(keyId);
    if (keyIdBytes.Length > ushort.MaxValue)
    {
      throw new CryptographicException("Encryption key id is too long.");
    }

    wrappedKey ??= [];
    if (wrappedKey.Length > ushort.MaxValue)
    {
      throw new CryptographicException("Wrapped key length exceeds payload limits.");
    }

    var payload = new byte[1 + 2 + keyIdBytes.Length + 2 + wrappedKey.Length + NonceLength + TagLength + encryptedData.Length];
    payload[0] = PayloadVersion;

    var cursor = 1;
    BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(cursor, 2), (ushort)keyIdBytes.Length);
    cursor += 2;
    keyIdBytes.CopyTo(payload.AsSpan(cursor, keyIdBytes.Length));
    cursor += keyIdBytes.Length;
    BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(cursor, 2), (ushort)wrappedKey.Length);
    cursor += 2;
    wrappedKey.CopyTo(payload.AsSpan(cursor, wrappedKey.Length));
    cursor += wrappedKey.Length;
    nonce.CopyTo(payload.AsSpan(cursor, NonceLength));
    cursor += NonceLength;
    tag.CopyTo(payload.AsSpan(cursor, TagLength));
    cursor += TagLength;
    encryptedData.CopyTo(payload.AsSpan(cursor, encryptedData.Length));

    return payload;
  }

  private static LocalKeyRing BuildLocalKeyRing(EncryptionOptions options)
  {
    var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    var v1Fallback = new List<byte[]>();
    var activeKeyId = ResolveActiveKeyId(options);

    var activeSecret = ResolveLocalSecret(options.LocalDataKey);
    var activeMaterial = SHA256.HashData(Encoding.UTF8.GetBytes(activeSecret));
    keys[activeKeyId] = activeMaterial;
    v1Fallback.Add(activeMaterial);

    foreach (var legacy in options.LegacyLocalDataKeys)
    {
      if (string.IsNullOrWhiteSpace(legacy.KeyId) || string.IsNullOrWhiteSpace(legacy.Secret))
      {
        continue;
      }

      var material = SHA256.HashData(Encoding.UTF8.GetBytes(legacy.Secret));
      keys[legacy.KeyId] = material;
      v1Fallback.Add(material);
    }

    return new LocalKeyRing(keys, v1Fallback.ToArray());
  }

  private static string ResolveLocalSecret(string? configuredKey)
  {
    if (!string.IsNullOrWhiteSpace(configuredKey)
      && configuredKey != "SET_VIA_USER_SECRETS_OR_ENV_VAR")
    {
      return configuredKey;
    }

    return "aarogya-dev-local-data-key";
  }

  private static string ResolveActiveKeyId(EncryptionOptions options)
  {
    if (!string.IsNullOrWhiteSpace(options.ActiveKeyId))
    {
      return options.ActiveKeyId;
    }

    return options.UseAwsKms
      ? options.KmsKeyId ?? "kms-primary"
      : "local-primary";
  }

  private sealed class LocalKeyRing(Dictionary<string, byte[]> keys, byte[][] v1FallbackKeys)
  {
    public Dictionary<string, byte[]> Keys { get; } = keys;

    public byte[][] V1FallbackKeys { get; } = v1FallbackKeys;
  }
}
