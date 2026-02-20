using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class VirusScanningOptions
{
  public const string SectionName = "VirusScanning";

  public bool EnableScanning { get; set; } = true;

  [Required]
  public string QuarantineBucketName { get; set; } = string.Empty;

  public string QuarantinePrefix { get; set; } = "quarantine";

  [Range(1, 1440)]
  public int DefinitionsRefreshIntervalMinutes { get; set; } = 60;
}
