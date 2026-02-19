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
  /// Secret material used to derive blind-index HMAC key.
  /// </summary>
  [Required]
  public string BlindIndexKey { get; set; } = "SET_VIA_USER_SECRETS_OR_ENV_VAR";
}
