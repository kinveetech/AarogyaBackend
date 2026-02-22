using System.ComponentModel;

namespace Aarogya.Domain.Enums;

public enum ReportStatus
{
  [Description("draft")] Draft,
  [Description("uploaded")] Uploaded,
  [Description("processing")] Processing,
  [Description("clean")] Clean,
  [Description("infected")] Infected,
  [Description("validated")] Validated,
  [Description("published")] Published,
  [Description("archived")] Archived
}
