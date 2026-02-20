using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class EmergencyAccessOptions
{
  public const string SectionName = "EmergencyAccess";

  public bool EnableAutoExpiryWorker { get; set; } = true;

  [Range(1, 60)]
  public int AutoExpiryWorkerIntervalMinutes { get; set; } = 5;

  [Range(1, 168)]
  public int DefaultDurationHours { get; set; } = 24;

  [Range(1, 168)]
  public int MinDurationHours { get; set; } = 24;

  [Range(1, 168)]
  public int MaxDurationHours { get; set; } = 48;

  [Range(1, 180)]
  public int PreExpiryNotificationLeadMinutes { get; set; } = 60;
}
