using Aarogya.Domain.Enums;

namespace Aarogya.Api.Features.V1.Reports;

internal static class ReportStatusMapper
{
  public static string ToStatusString(ReportStatus status)
  {
    return status switch
    {
      ReportStatus.Draft => "draft",
      ReportStatus.Uploaded => "uploaded",
      ReportStatus.Processing => "processing",
      ReportStatus.Clean => "clean",
      ReportStatus.Infected => "infected",
      ReportStatus.Validated => "validated",
      ReportStatus.Published => "published",
      ReportStatus.Archived => "archived",
      ReportStatus.Extracting => "extracting",
      ReportStatus.Extracted => "extracted",
      ReportStatus.ExtractionFailed => "extraction_failed",
      _ => "uploaded"
    };
  }
}
