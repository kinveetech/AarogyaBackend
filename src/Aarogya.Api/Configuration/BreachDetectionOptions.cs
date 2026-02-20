using System.ComponentModel.DataAnnotations;

namespace Aarogya.Api.Configuration;

public sealed class BreachDetectionOptions
{
  public const string SectionName = "BreachDetection";

  public bool EnableWorker { get; init; } = true;

  [Range(1, 60)]
  public int ScanIntervalMinutes { get; init; } = 1;

  [Range(1, 1440)]
  public int LookbackWindowMinutes { get; init; } = 15;

  [Range(1, 10000)]
  public int SuspiciousAccessThresholdPerActor { get; init; } = 25;

  [Range(1, 10000)]
  public int BulkExportThresholdPerActor { get; init; } = 50;

  [Required]
  public IReadOnlyList<string> SuspiciousActions { get; init; } =
  [
    "report.viewed",
    "report.listed",
    "emergency_access.report_viewed",
    "emergency_access.report_listed"
  ];

  [Required]
  public IReadOnlyList<string> BulkExportActions { get; init; } =
  [
    "report.viewed",
    "report.downloaded",
    "report.download_url.created"
  ];

  public bool NotifyImpactedUsers { get; init; } = true;

  public bool NotifyAuthorities { get; init; } = true;

  public IReadOnlyList<string> AuthorityEmails { get; init; } = [];
}
