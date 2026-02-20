using System.Collections.Concurrent;
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

  public IReadOnlyCollection<string> GetAssignedRoles(string userSub);
}

internal sealed class InMemoryRoleAssignmentService : IRoleAssignmentService
{
  private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _userRoles = new(StringComparer.Ordinal);

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

    var assignedRoles = _userRoles.GetOrAdd(targetSub, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
    assignedRoles[normalizedRole] = 0;

    message = $"Role '{normalizedRole}' assigned to user '{targetSub}'.";
    return true;
  }

  public IReadOnlyCollection<string> GetAssignedRoles(string userSub)
  {
    if (string.IsNullOrWhiteSpace(userSub))
    {
      return Array.Empty<string>();
    }

    return _userRoles.TryGetValue(userSub, out var roles)
      ? roles.Keys.ToArray()
      : Array.Empty<string>();
  }
}
