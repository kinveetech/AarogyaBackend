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
