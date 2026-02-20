using Aarogya.Api.Authentication;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;

namespace Aarogya.Api.Features.V1.Users;

internal sealed class UserProfileService(
  IUserRepository userRepository,
  IUnitOfWork unitOfWork,
  IUtcClock clock)
  : IUserProfileService
{
  public async Task<UserProfileResponse> GetCurrentUserAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new KeyNotFoundException("Authenticated user is not provisioned in the database.");

    return ToResponse(user);
  }

  public async Task<UserProfileResponse> UpdateCurrentUserAsync(
    string userSub,
    UpdateUserProfileRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new KeyNotFoundException("Authenticated user is not provisioned in the database.");

    if (request.FirstName is not null)
    {
      user.FirstName = request.FirstName.Trim();
    }

    if (request.LastName is not null)
    {
      user.LastName = request.LastName.Trim();
    }

    if (request.Email is not null)
    {
      user.Email = request.Email.Trim();
    }

    if (request.Phone is not null)
    {
      user.Phone = request.Phone.Trim();
    }

    if (request.Address is not null)
    {
      user.Address = request.Address.Trim();
    }

    if (request.BloodGroup is not null)
    {
      user.BloodGroup = request.BloodGroup.Trim().ToUpperInvariant();
    }

    if (request.DateOfBirth.HasValue)
    {
      user.DateOfBirth = request.DateOfBirth;
    }

    user.UpdatedAt = clock.UtcNow;

    userRepository.Update(user);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return ToResponse(user);
  }

  private static UserProfileResponse ToResponse(Domain.Entities.User user)
  {
    return new UserProfileResponse(
      user.ExternalAuthId ?? string.Empty,
      user.Email,
      user.FirstName,
      user.LastName,
      user.Phone,
      user.Address,
      user.BloodGroup,
      user.DateOfBirth,
      [ToRoleName(user.Role)]);
  }

  private static string ToRoleName(UserRole role)
  {
    return role switch
    {
      UserRole.Patient => "Patient",
      UserRole.Doctor => "Doctor",
      UserRole.LabTechnician => "LabTechnician",
      UserRole.Admin => "Admin",
      _ => role.ToString()
    };
  }
}
