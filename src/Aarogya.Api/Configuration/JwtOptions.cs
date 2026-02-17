using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class JwtOptions
{
  public const string SectionName = "Jwt";

  [Required]
  [MinLength(32, ErrorMessage = "JWT Key must be at least 32 characters for HMAC-SHA256")]
  public string Key { get; set; } = string.Empty;

  [Required]
  public string Issuer { get; set; } = "AarogyaAPI";

  [Required]
  public string Audience { get; set; } = "AarogyaClients";

  [Range(1, 1440)]
  public int ExpiryInMinutes { get; set; } = 60;
}
