using System.ComponentModel;

namespace Aarogya.Domain.Enums;

public enum AccessGrantStatus
{
  [Description("active")] Active,
  [Description("revoked")] Revoked,
  [Description("expired")] Expired
}
