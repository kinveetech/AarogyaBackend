using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class SecurityHeadersOptions
{
  public const string SectionName = "SecurityHeaders";

  [Required]
  public string ContentSecurityPolicy { get; set; } = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'; object-src 'none'";

  [Required]
  public string XFrameOptions { get; set; } = "DENY";

  [Required]
  public string XContentTypeOptions { get; set; } = "nosniff";

  [Required]
  public string ReferrerPolicy { get; set; } = "no-referrer";

  public bool HstsIncludeSubDomains { get; set; } = true;

  public bool HstsPreload { get; set; } = true;

  [Range(1, 3650)]
  public int HstsMaxAgeDays { get; set; } = 730;
}
