using System.ComponentModel;

namespace Aarogya.Domain.Enums;

public enum ReportType
{
  [Description("blood_test")] BloodTest,
  [Description("urine_test")] UrineTest,
  [Description("radiology")] Radiology,
  [Description("cardiology")] Cardiology,
  [Description("other")] Other
}
