using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class RateLimitingOptions
{
  public const string SectionName = "RateLimiting";

  public bool EnableRateLimiting { get; set; } = true;

  [Required]
  public RateLimitPolicyOptions Auth { get; set; } = new();

  [Required]
  public RateLimitPolicyOptions ApiV1 { get; set; } = new();
}

public sealed class RateLimitPolicyOptions
{
  [Required]
  [RegularExpression("^(fixed|sliding)$", ErrorMessage = "Strategy must be either 'fixed' or 'sliding'.")]
  public string Strategy { get; set; } = "fixed";

  [Range(1, 10_000)]
  public int PermitLimit { get; set; } = 120;

  [Range(1, 86_400)]
  public int WindowSeconds { get; set; } = 60;

  [Range(1, 64)]
  public int SegmentsPerWindow { get; set; } = 4;

  [Range(0, 1_000)]
  public int QueueLimit { get; set; }

  public bool PreferPerUserLimits { get; set; } = true;
}
