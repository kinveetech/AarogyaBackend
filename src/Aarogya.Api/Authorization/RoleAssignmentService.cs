using System.Diagnostics.CodeAnalysis;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;

namespace Aarogya.Api.Authorization;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public controller constructor injection.")]
public interface IRoleAssignmentService
{
  public Task<(bool Success, string Message)> TryAssignRoleAsync(
    string actorSub,
    IReadOnlyCollection<string> actorRoles,
    string targetSub,
    string targetRole,
    CancellationToken cancellationToken = default);

  public Task<IReadOnlyCollection<string>> GetAssignedRolesAsync(
    string userSub,
    CancellationToken cancellationToken = default);
}

internal sealed class DatabaseRoleAssignmentService(
  IUserRepository userRepository,
  IUnitOfWork unitOfWork) : IRoleAssignmentService
{
  public async Task<(bool Success, string Message)> TryAssignRoleAsync(
    string actorSub,
    IReadOnlyCollection<string> actorRoles,
    string targetSub,
    string targetRole,
    CancellationToken cancellationToken = default)
  {
    if (!actorRoles.Contains(AarogyaRoles.Admin, StringComparer.OrdinalIgnoreCase))
    {
      return (false, "Only admins can assign roles.");
    }

    if (!AarogyaRoles.All.Contains(targetRole, StringComparer.OrdinalIgnoreCase))
    {
      return (false, "Unknown role.");
    }

    var normalizedRole = AarogyaRoles.All.First(
      role => string.Equals(role, targetRole, StringComparison.OrdinalIgnoreCase));
    var isSelfUpdate = string.Equals(actorSub, targetSub, StringComparison.Ordinal);
    var alreadyHasRole = actorRoles.Contains(normalizedRole, StringComparer.OrdinalIgnoreCase);

    if (isSelfUpdate && !alreadyHasRole)
    {
      return (false, "Cannot escalate your own role.");
    }

    var user = await userRepository.GetByExternalAuthIdAsync(targetSub, cancellationToken);
    if (user is null)
    {
      return (false, $"User '{targetSub}' not found.");
    }

    if (!Enum.TryParse<UserRole>(normalizedRole, ignoreCase: true, out var parsedRole))
    {
      return (false, $"Role '{normalizedRole}' cannot be mapped to a valid user role.");
    }

    user.Role = parsedRole;
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return (true, $"Role '{normalizedRole}' assigned to user '{targetSub}'.");
  }

  public async Task<IReadOnlyCollection<string>> GetAssignedRolesAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(userSub))
    {
      return [];
    }

    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken);
    if (user is null)
    {
      return [];
    }

    return [user.Role.ToString()];
  }
}
