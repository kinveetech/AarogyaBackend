using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class AccessGrantOptions
{
  public const string SectionName = "AccessGrants";

  [Range(1, 365)]
  public int DefaultExpiryDays { get; set; } = 30;

  [Range(1, 365)]
  public int MaxExpiryDays { get; set; } = 180;
}
