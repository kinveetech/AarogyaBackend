using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class OtpOptions
{
  public const string SectionName = "Otp";

  [Range(4, 8)]
  public int CodeLength { get; set; } = 6;

  [Range(30, 900)]
  public int CodeExpirySeconds { get; set; } = 300;

  [Range(1, 10)]
  public int MaxRequestsPerWindow { get; set; } = 3;

  [Range(60, 3600)]
  public int RateLimitWindowSeconds { get; set; } = 600;
}
