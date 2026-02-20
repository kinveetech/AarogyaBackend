using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class ApiKeyOptions
{
  public const string SectionName = "ApiKeys";

  [Required]
  public string KeyPrefix { get; set; } = "aarogya_lab_";

  [Range(1, 100000)]
  public int MaxRequestsPerWindow { get; set; } = 120;

  [Range(1, 3600)]
  public int RateLimitWindowSeconds { get; set; } = 60;

  [Range(1, 3650)]
  public int DefaultKeyLifetimeDays { get; set; } = 365;

  [Range(1, 10080)]
  public int RotationOverlapMinutes { get; set; } = 1440;
}
