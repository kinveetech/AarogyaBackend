using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Authorization;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IRoleAssignmentService
{
  public bool TryAssignRole(
    string actorSub,
    IReadOnlyCollection<string> actorRoles,
    string targetSub,
    string targetRole,
    out string message);
}

internal sealed class InMemoryRoleAssignmentService : IRoleAssignmentService
{
  public bool TryAssignRole(
    string actorSub,
    IReadOnlyCollection<string> actorRoles,
    string targetSub,
    string targetRole,
    out string message)
  {
    if (!actorRoles.Contains(AarogyaRoles.Admin, StringComparer.OrdinalIgnoreCase))
    {
      message = "Only admins can assign roles.";
      return false;
    }

    if (!AarogyaRoles.All.Contains(targetRole, StringComparer.OrdinalIgnoreCase))
    {
      message = "Unknown role.";
      return false;
    }

    var normalizedRole = AarogyaRoles.All.First(role => string.Equals(role, targetRole, StringComparison.OrdinalIgnoreCase));
    var isSelfUpdate = string.Equals(actorSub, targetSub, StringComparison.Ordinal);
    var alreadyHasRole = actorRoles.Contains(normalizedRole, StringComparer.OrdinalIgnoreCase);

    if (isSelfUpdate && !alreadyHasRole)
    {
      message = "Cannot escalate your own role.";
      return false;
    }

    message = $"Role '{normalizedRole}' assigned to user '{targetSub}'.";
    return true;
  }
}
