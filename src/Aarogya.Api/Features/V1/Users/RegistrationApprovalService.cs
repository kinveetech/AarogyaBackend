using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;

namespace Aarogya.Api.Features.V1.Users;

internal sealed class RegistrationApprovalService(
  IUserRepository userRepository,
  IDoctorProfileRepository doctorProfileRepository,
  ILabTechnicianProfileRepository labTechnicianProfileRepository,
  IUnitOfWork unitOfWork,
  IAuditLoggingService auditLoggingService,
  IUtcClock clock)
  : IRegistrationApprovalService
{
  public async Task<IReadOnlyList<PendingRegistrationResponse>> ListPendingAsync(
    CancellationToken cancellationToken = default)
  {
    var pendingUsers = await userRepository.ListAsync(
      new UsersByRegistrationStatusSpecification(RegistrationStatus.PendingApproval),
      cancellationToken);

    var results = new List<PendingRegistrationResponse>(pendingUsers.Count);

    foreach (var user in pendingUsers)
    {
      DoctorRegistrationData? doctorData = null;
      LabTechnicianRegistrationData? labData = null;

      if (user.Role == UserRole.Doctor)
      {
        var profile = await doctorProfileRepository.GetByUserIdAsync(user.Id, cancellationToken);
        if (profile is not null)
        {
          doctorData = new DoctorRegistrationData(
            profile.MedicalLicenseNumber,
            profile.Specialization,
            profile.ClinicOrHospitalName,
            profile.ClinicAddress);
        }
      }
      else if (user.Role == UserRole.LabTechnician)
      {
        var profile = await labTechnicianProfileRepository.GetByUserIdAsync(user.Id, cancellationToken);
        if (profile is not null)
        {
          labData = new LabTechnicianRegistrationData(
            profile.LabName,
            profile.LabLicenseNumber,
            profile.NablAccreditationId);
        }
      }

      results.Add(new PendingRegistrationResponse(
        user.ExternalAuthId ?? string.Empty,
        ToRoleName(user.Role),
        user.FirstName,
        user.LastName,
        user.Email,
        user.CreatedAt,
        doctorData,
        labData));
    }

    return results;
  }

  public async Task<RegistrationStatusResponse> ApproveAsync(
    string adminSub,
    string targetSub,
    ApproveRegistrationRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var admin = await userRepository.GetByExternalAuthIdAsync(adminSub, cancellationToken)
      ?? throw new KeyNotFoundException("Admin user not found.");

    var targetUser = await userRepository.GetByExternalAuthIdAsync(targetSub, cancellationToken)
      ?? throw new KeyNotFoundException($"User '{targetSub}' not found.");

    if (targetUser.RegistrationStatus != RegistrationStatus.PendingApproval)
    {
      throw new InvalidOperationException(
        $"Cannot approve a registration with status '{targetUser.RegistrationStatus}'.");
    }

    targetUser.RegistrationStatus = RegistrationStatus.Approved;
    targetUser.IsActive = true;
    targetUser.UpdatedAt = clock.UtcNow;

    userRepository.Update(targetUser);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    await auditLoggingService.LogDataAccessAsync(
      admin,
      "user_registration.approved",
      "user",
      targetUser.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["targetSub"] = targetSub,
        ["role"] = targetUser.Role.ToString()
      },
      cancellationToken);

    return new RegistrationStatusResponse(
      targetUser.ExternalAuthId ?? string.Empty,
      ToRoleName(targetUser.Role),
      "approved",
      null);
  }

  public async Task<RegistrationStatusResponse> RejectAsync(
    string adminSub,
    string targetSub,
    RejectRegistrationRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var admin = await userRepository.GetByExternalAuthIdAsync(adminSub, cancellationToken)
      ?? throw new KeyNotFoundException("Admin user not found.");

    var targetUser = await userRepository.GetByExternalAuthIdAsync(targetSub, cancellationToken)
      ?? throw new KeyNotFoundException($"User '{targetSub}' not found.");

    if (targetUser.RegistrationStatus != RegistrationStatus.PendingApproval)
    {
      throw new InvalidOperationException(
        $"Cannot reject a registration with status '{targetUser.RegistrationStatus}'.");
    }

    targetUser.RegistrationStatus = RegistrationStatus.Rejected;
    targetUser.IsActive = false;
    targetUser.UpdatedAt = clock.UtcNow;

    userRepository.Update(targetUser);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    await auditLoggingService.LogDataAccessAsync(
      admin,
      "user_registration.rejected",
      "user",
      targetUser.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["targetSub"] = targetSub,
        ["role"] = targetUser.Role.ToString(),
        ["reason"] = request.Reason
      },
      cancellationToken);

    return new RegistrationStatusResponse(
      targetUser.ExternalAuthId ?? string.Empty,
      ToRoleName(targetUser.Role),
      "rejected",
      request.Reason);
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
