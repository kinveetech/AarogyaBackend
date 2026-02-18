using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class RedisOptions
{
  public const string SectionName = "Redis";

  [Required]
  public string InstanceName { get; set; } = "aarogya_";

  [Range(0, 15)]
  public int Database { get; set; } = 0;

  [Range(100, 60000)]
  public int ConnectTimeoutMilliseconds { get; set; } = 5000;

  [Range(1, 10)]
  public int ConnectRetry { get; set; } = 3;

  [Range(100, 60000)]
  public int SyncTimeoutMilliseconds { get; set; } = 5000;

  [Range(1, 10080)]
  public int DefaultExpirationMinutes { get; set; } = 30;

  [Required]
  public string KeyPrefix { get; set; } = "aarogya:default";

  public string? Password { get; set; }
}
