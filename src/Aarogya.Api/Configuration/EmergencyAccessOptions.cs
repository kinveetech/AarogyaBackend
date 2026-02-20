using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class EmergencyAccessOptions
{
  public const string SectionName = "EmergencyAccess";

  [Range(1, 168)]
  public int DefaultDurationHours { get; set; } = 24;

  [Range(1, 168)]
  public int MinDurationHours { get; set; } = 24;

  [Range(1, 168)]
  public int MaxDurationHours { get; set; } = 48;
}
