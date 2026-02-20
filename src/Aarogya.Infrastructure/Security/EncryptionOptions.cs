using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Aarogya.Infrastructure.Security;

public sealed class EncryptionOptions
{
  public const string SectionName = "Encryption";

  /// <summary>
  /// Enables AWS KMS data-key generation. Disable for local fallback mode.
  /// </summary>
  public bool UseAwsKms { get; set; } = true;

  /// <summary>
  /// Symmetric KMS key id/arn used for GenerateDataKey.
  /// </summary>
  public string? KmsKeyId { get; set; }

  /// <summary>
  /// Optional fallback secret used to derive a local AES-256 key when KMS is disabled.
  /// </summary>
  public string? LocalDataKey { get; set; }

  /// <summary>
  /// Logical key identifier stamped into encrypted payloads.
  /// </summary>
  public string ActiveKeyId { get; set; } = "local-primary";

  /// <summary>
  /// Legacy local keys kept for backward decryption during/after rotation.
  /// </summary>
  public Collection<LegacyEncryptionKeyOptions> LegacyLocalDataKeys { get; set; } = [];

  /// <summary>
  /// Secret material used to derive blind-index HMAC key.
  /// </summary>
  [Required]
  public string BlindIndexKey { get; set; } = "SET_VIA_USER_SECRETS_OR_ENV_VAR";
}

public sealed class LegacyEncryptionKeyOptions
{
  [Required]
  public string KeyId { get; set; } = string.Empty;

  [Required]
  public string Secret { get; set; } = string.Empty;
}
