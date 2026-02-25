using System.Diagnostics.CodeAnalysis;
using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;

namespace Aarogya.Api.Features.V1.Users;

[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Gender values are stored as lowercase per database check constraint.")]
internal sealed class UserRegistrationService(
  IUserRepository userRepository,
  IDoctorProfileRepository doctorProfileRepository,
  ILabTechnicianProfileRepository labTechnicianProfileRepository,
  IConsentRecordRepository consentRecordRepository,
  IUnitOfWork unitOfWork,
  IAuditLoggingService auditLoggingService,
  IUtcClock clock)
  : IUserRegistrationService
{
  private static readonly Dictionary<string, UserRole> RoleMap = new(StringComparer.OrdinalIgnoreCase)
  {
    ["patient"] = UserRole.Patient,
    ["doctor"] = UserRole.Doctor,
    ["lab_technician"] = UserRole.LabTechnician
  };

  public async Task<RegisterUserResponse> RegisterAsync(
    string userSub,
    RegisterUserRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var existing = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken);
    if (existing is not null)
    {
      throw new InvalidOperationException("User is already registered.");
    }

    if (!RoleMap.TryGetValue(request.Role.Trim(), out var role))
    {
      throw new InvalidOperationException($"Unsupported role '{request.Role}'.");
    }

    var registrationStatus = role == UserRole.Patient
      ? RegistrationStatus.Approved
      : RegistrationStatus.PendingApproval;

    var now = clock.UtcNow;
    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = userSub,
      Role = role,
      FirstName = InputSanitizer.SanitizePlainText(request.FirstName),
      LastName = InputSanitizer.SanitizePlainText(request.LastName),
      Email = InputSanitizer.SanitizePlainText(request.Email),
      Phone = InputSanitizer.SanitizeNullablePlainText(request.Phone),
      Address = InputSanitizer.SanitizeNullablePlainText(request.Address),
      BloodGroup = InputSanitizer.SanitizeNullablePlainText(request.BloodGroup)?.ToUpperInvariant(),
      DateOfBirth = request.DateOfBirth,
      Gender = InputSanitizer.SanitizeNullablePlainText(request.Gender)?.ToLowerInvariant(),
      RegistrationStatus = registrationStatus,
      IsActive = role == UserRole.Patient,
      CreatedAt = now,
      UpdatedAt = now
    };

    await userRepository.AddAsync(user, cancellationToken);

    if (role == UserRole.Doctor)
    {
      var doctorData = request.DoctorData
        ?? throw new InvalidOperationException("DoctorData is required for doctor registration.");

      var doctorProfile = new DoctorProfile
      {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        MedicalLicenseNumber = InputSanitizer.SanitizePlainText(doctorData.MedicalLicenseNumber),
        Specialization = InputSanitizer.SanitizePlainText(doctorData.Specialization),
        ClinicOrHospitalName = InputSanitizer.SanitizeNullablePlainText(doctorData.ClinicOrHospitalName),
        ClinicAddress = InputSanitizer.SanitizeNullablePlainText(doctorData.ClinicAddress),
        CreatedAt = now,
        UpdatedAt = now
      };

      await doctorProfileRepository.AddAsync(doctorProfile, cancellationToken);
    }
    else if (role == UserRole.LabTechnician)
    {
      var labData = request.LabTechnicianData
        ?? throw new InvalidOperationException("LabTechnicianData is required for lab technician registration.");

      var labProfile = new LabTechnicianProfile
      {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        LabName = InputSanitizer.SanitizePlainText(labData.LabName),
        LabLicenseNumber = InputSanitizer.SanitizeNullablePlainText(labData.LabLicenseNumber),
        NablAccreditationId = InputSanitizer.SanitizeNullablePlainText(labData.NablAccreditationId),
        CreatedAt = now,
        UpdatedAt = now
      };

      await labTechnicianProfileRepository.AddAsync(labProfile, cancellationToken);
    }

    var consentsGranted = await CreateInitialConsentsAsync(user, request.Consents, now, cancellationToken);

    await unitOfWork.SaveChangesAsync(cancellationToken);

    await auditLoggingService.LogDataAccessAsync(
      user,
      "user_registration.completed",
      "user",
      user.Id,
      200,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["role"] = request.Role,
        ["registrationStatus"] = registrationStatus.ToString()
      },
      cancellationToken);

    return new RegisterUserResponse(
      user.ExternalAuthId ?? string.Empty,
      ToRoleName(user.Role),
      ToRegistrationStatusName(registrationStatus),
      user.Email,
      user.FirstName,
      user.LastName,
      user.Phone,
      user.Address,
      user.BloodGroup,
      user.DateOfBirth,
      user.Gender,
      consentsGranted);
  }

  public async Task<RegistrationStatusResponse> GetRegistrationStatusAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken);
    if (user is null)
    {
      return new RegistrationStatusResponse(userSub, string.Empty, "registration_required", null);
    }

    return new RegistrationStatusResponse(
      user.ExternalAuthId ?? string.Empty,
      ToRoleName(user.Role),
      ToRegistrationStatusName(user.RegistrationStatus),
      null);
  }

  private async Task<IReadOnlyList<string>> CreateInitialConsentsAsync(
    User user,
    IReadOnlyList<InitialConsentGrant>? consents,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    if (consents is null || consents.Count == 0)
    {
      return [];
    }

    var granted = new List<string>();

    foreach (var consent in consents)
    {
      var purpose = InputSanitizer.SanitizePlainText(consent.Purpose).Trim();
      if (!ConsentPurposeCatalog.IsSupported(purpose))
      {
        throw new InvalidOperationException($"Unsupported consent purpose '{purpose}'.");
      }

      var normalizedPurpose = ConsentPurposeCatalog.All.First(
        x => string.Equals(x, purpose, StringComparison.OrdinalIgnoreCase));

      var record = new ConsentRecord
      {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Purpose = normalizedPurpose,
        IsGranted = consent.IsGranted,
        Source = "registration",
        OccurredAt = now,
        CreatedAt = now,
        UpdatedAt = now
      };

      await consentRecordRepository.AddAsync(record, cancellationToken);

      if (consent.IsGranted)
      {
        granted.Add(normalizedPurpose);
      }
    }

    return granted;
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

  private static string ToRegistrationStatusName(RegistrationStatus status)
  {
    return status switch
    {
      RegistrationStatus.PendingApproval => "pending_approval",
      RegistrationStatus.Approved => "approved",
      RegistrationStatus.Rejected => "rejected",
      _ => status.ToString()
    };
  }
}
