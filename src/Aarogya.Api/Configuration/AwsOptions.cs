using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class AwsOptions
{
  public const string SectionName = "Aws";

  [Required]
  public string Region { get; set; } = "ap-south-1";

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration binding requires string type")]
  public string? ServiceUrl { get; set; }

  public bool UseLocalStack { get; set; }

  public string AccessKey { get; set; } = string.Empty;

  public string SecretKey { get; set; } = string.Empty;

  public S3Options S3 { get; set; } = new();

  public SesOptions Ses { get; set; } = new();

  public CognitoOptions Cognito { get; set; } = new();
}

public sealed class S3Options
{
  [Required]
  public string BucketName { get; set; } = string.Empty;

  [Range(1, 10080)]
  public int PresignedUrlExpiryMinutes { get; set; } = 60;

  /// <summary>
  /// Default object access: "private" or "public-read".
  /// </summary>
  public string DefaultAcl { get; set; } = "private";
}

public sealed class SesOptions
{
  [Required]
  [EmailAddress]
  public string SenderEmail { get; set; } = string.Empty;

  [Required]
  public string SenderName { get; set; } = string.Empty;
}

public sealed class CognitoOptions
{
  [Required]
  public string UserPoolName { get; set; } = string.Empty;

  public string? UserPoolId { get; set; }

  public string? AppClientId { get; set; }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration binding requires string type")]
  public string? Issuer { get; set; }

  [RegularExpression("^(OFF|ON|OPTIONAL)$", ErrorMessage = "MfaConfiguration must be OFF, ON, or OPTIONAL.")]
  public string MfaConfiguration { get; set; } = "OPTIONAL";

  public CognitoPasswordPolicyOptions PasswordPolicy { get; set; } = new();
}

public sealed class CognitoPasswordPolicyOptions
{
  [Range(8, 99)]
  public int MinimumLength { get; set; } = 8;

  public bool RequireLowercase { get; set; } = true;

  public bool RequireUppercase { get; set; } = true;

  public bool RequireNumbers { get; set; } = true;

  public bool RequireSymbols { get; set; } = true;
}
