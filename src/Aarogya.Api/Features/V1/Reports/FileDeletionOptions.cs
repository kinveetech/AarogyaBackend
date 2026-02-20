using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class FileDeletionOptions
{
  public const string SectionName = "FileDeletion";

  public bool EnableHardDeleteWorker { get; set; } = true;

  [Range(1, 3650)]
  public int RetentionDays { get; set; } = 2555;

  [Range(1, 1440)]
  public int WorkerIntervalMinutes { get; set; } = 60;
}
