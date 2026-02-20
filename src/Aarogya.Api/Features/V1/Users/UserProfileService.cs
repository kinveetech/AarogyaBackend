using Aarogya.Api.Authentication;
using Aarogya.Api.Security;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Infrastructure.Aadhaar;

namespace Aarogya.Api.Features.V1.Users;

internal sealed class UserProfileService(
  IUserRepository userRepository,
  IUnitOfWork unitOfWork,
  IAadhaarVaultService aadhaarVaultService,
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
      user.FirstName = InputSanitizer.SanitizePlainText(request.FirstName);
    }

    if (request.LastName is not null)
    {
      user.LastName = InputSanitizer.SanitizePlainText(request.LastName);
    }

    if (request.Email is not null)
    {
      user.Email = InputSanitizer.SanitizePlainText(request.Email);
    }

    if (request.Phone is not null)
    {
      user.Phone = InputSanitizer.SanitizePlainText(request.Phone);
    }

    if (request.Address is not null)
    {
      user.Address = InputSanitizer.SanitizePlainText(request.Address);
    }

    if (request.BloodGroup is not null)
    {
      user.BloodGroup = InputSanitizer.SanitizePlainText(request.BloodGroup).ToUpperInvariant();
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

  public async Task<AadhaarVerificationResponse> VerifyCurrentUserAadhaarAsync(
    string userSub,
    VerifyAadhaarRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new KeyNotFoundException("Authenticated user is not provisioned in the database.");

    if (user.Role != UserRole.Patient)
    {
      throw new InvalidOperationException("Only patient profiles can attach Aadhaar verification.");
    }

    var normalizedAadhaar = AadhaarHashing.Normalize(request.AadhaarNumber);
    var verification = await aadhaarVaultService.VerifyAndCreateReferenceTokenAsync(normalizedAadhaar, user.Id, cancellationToken);

    user.AadhaarRefToken = verification.ReferenceToken;
    user.AadhaarSha256 = AadhaarHashing.ComputeSha256(normalizedAadhaar);
    user.UpdatedAt = clock.UtcNow;

    userRepository.Update(user);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return new AadhaarVerificationResponse(
      verification.ReferenceToken,
      verification.IsExistingRecord,
      verification.Provider,
      verification.Demographics is null
        ? null
        : new AadhaarDemographicsResponse(
          verification.Demographics.FullName,
          verification.Demographics.DateOfBirth,
          verification.Demographics.Gender,
          verification.Demographics.Address));
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
