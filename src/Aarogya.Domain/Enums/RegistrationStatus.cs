using System.ComponentModel;

namespace Aarogya.Domain.Enums;

public enum RegistrationStatus
{
  [Description("pending_approval")] PendingApproval,
  [Description("approved")] Approved,
  [Description("rejected")] Rejected
}
