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

  public SqsOptions Sqs { get; set; } = new();

  public SesOptions Ses { get; set; } = new();

  public CognitoOptions Cognito { get; set; } = new();
}

public sealed class S3Options
{
  [Required]
  public string BucketName { get; set; } = string.Empty;

  [Range(1, 10080)]
  public int PresignedUrlExpiryMinutes { get; set; } = 15;

  /// <summary>
  /// Default object access: "private" or "public-read".
  /// </summary>
  public string DefaultAcl { get; set; } = "private";

  public CloudFrontOptions CloudFront { get; set; } = new();
}

public sealed class CloudFrontOptions
{
  public bool Enabled { get; set; }

  public string? DistributionId { get; set; }

  public string? DistributionDomain { get; set; }

  public string? KeyPairId { get; set; }

  public string? PrivateKeyPem { get; set; }

  public bool EnableInvalidationOnDelete { get; set; } = true;
}

public sealed class SesOptions
{
  [Required]
  [EmailAddress]
  public string SenderEmail { get; set; } = string.Empty;

  [Required]
  public string SenderName { get; set; } = string.Empty;
}

public sealed class SqsOptions
{
  [Required]
  public string QueueName { get; set; } = "aarogya-dev-queue";

  public bool ConfigureS3NotificationsOnStartup { get; set; } = true;

  public bool EnableUploadEventConsumer { get; set; } = true;

  [Range(1, 10)]
  public int MaxNumberOfMessages { get; set; } = 5;

  [Range(1, 20)]
  public int ReceiveWaitTimeSeconds { get; set; } = 10;

  [Range(1, 43200)]
  public int VisibilityTimeoutSeconds { get; set; } = 30;

  [Range(1, 300)]
  public int EmptyPollDelayMilliseconds { get; set; } = 250;
}

public sealed class CognitoOptions
{
  [Required]
  public string UserPoolName { get; set; } = string.Empty;

  public string? UserPoolId { get; set; }

  public string? AppClientId { get; set; }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration binding requires string type")]
  public string? Issuer { get; set; }

  public CognitoSocialIdentityProviderOptions SocialIdentityProviders { get; set; } = new();

  [RegularExpression("^(OFF|ON|OPTIONAL)$", ErrorMessage = "MfaConfiguration must be OFF, ON, or OPTIONAL.")]
  public string MfaConfiguration { get; set; } = "OPTIONAL";

  public CognitoPasswordPolicyOptions PasswordPolicy { get; set; } = new();
}

public sealed class CognitoSocialIdentityProviderOptions
{
  public SocialProviderOptions Google { get; set; } = new();

  public SocialProviderOptions Apple { get; set; } = new();

  public SocialProviderOptions Facebook { get; set; } = new();

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Configuration binding requires mutable collection.")]
  public List<string> MobileRedirectUris { get; set; } = [];
}

public sealed class SocialProviderOptions
{
  public bool Enabled { get; set; }

  public string? ClientId { get; set; }

  public string? ClientSecret { get; set; }

  [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Configuration binding requires mutable collection.")]
  public List<string> Scopes { get; set; } = ["openid", "email", "profile"];

  public SocialAttributeMappingOptions AttributeMapping { get; set; } = new();
}

public sealed class SocialAttributeMappingOptions
{
  public string Email { get; set; } = "email";

  public string GivenName { get; set; } = "given_name";

  public string FamilyName { get; set; } = "family_name";
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
