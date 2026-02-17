using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class RedisOptions
{
  public const string SectionName = "Redis";

  [Required]
  public string InstanceName { get; set; } = "aarogya_";

  [Range(1, 10080)]
  public int DefaultExpirationMinutes { get; set; } = 30;

  public string? Password { get; set; }
}
