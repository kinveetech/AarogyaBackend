using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class SmsNotificationsOptions
{
  public const string SectionName = "SmsNotifications";

  public bool EnableCriticalSms { get; set; }

  [Range(1, 20)]
  public int MaxSendsPerWindow { get; set; } = 5;

  [Range(1, 3600)]
  public int RateLimitWindowSeconds { get; set; } = 60;

  [Range(typeof(decimal), "0", "1000")]
  public decimal EstimatedCostPerMessageInInr { get; set; } = 0.25m;
}
