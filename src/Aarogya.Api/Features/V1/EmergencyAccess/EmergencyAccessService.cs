using Aarogya.Api.Auditing;
using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Api.Security;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.EmergencyAccess;

internal sealed class EmergencyAccessService(
  IUserRepository userRepository,
  IEmergencyContactRepository emergencyContactRepository,
  IAccessGrantRepository accessGrantRepository,
  IUnitOfWork unitOfWork,
  IAuditLoggingService auditLoggingService,
  ITransactionalEmailNotificationService transactionalEmailNotificationService,
  ICriticalSmsNotificationService criticalSmsNotificationService,
  IPushNotificationService pushNotificationService,
  IOptions<EmergencyAccessOptions> options,
  IUtcClock clock)
  : IEmergencyAccessService
{
  private readonly EmergencyAccessOptions _options = options.Value;

  public async Task<EmergencyAccessResponse> RequestAsync(
    CreateEmergencyAccessRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var patientSub = InputSanitizer.SanitizePlainText(request.PatientSub);
    var doctorSub = InputSanitizer.SanitizePlainText(request.DoctorSub);
    var reason = InputSanitizer.SanitizePlainText(request.Reason);

    var patient = await ResolveUserAsync(patientSub, UserRole.Patient, "patient", cancellationToken);
    var doctor = await ResolveUserAsync(doctorSub, UserRole.Doctor, "doctor", cancellationToken);
    var contact = await ResolveEmergencyContactAsync(patient.Id, request.EmergencyContactPhone, cancellationToken);

    var now = clock.UtcNow;
    var durationHours = request.DurationHours ?? _options.DefaultDurationHours;
    if (durationHours < _options.MinDurationHours || durationHours > _options.MaxDurationHours)
    {
      throw new InvalidOperationException($"DurationHours must be between {_options.MinDurationHours} and {_options.MaxDurationHours}.");
    }

    var existingGrant = await accessGrantRepository.GetActiveGrantAsync(patient.Id, doctor.Id, cancellationToken);
    if (existingGrant is not null)
    {
      existingGrant.Status = AccessGrantStatus.Revoked;
      existingGrant.RevokedAt = now;
      accessGrantRepository.Update(existingGrant);
    }

    var expiresAt = now.AddHours(durationHours);
    var grant = new AccessGrant
    {
      Id = Guid.NewGuid(),
      PatientId = patient.Id,
      GrantedToUserId = doctor.Id,
      GrantedByUserId = patient.Id,
      GrantReason = $"emergency:{reason}",
      Scope = new AccessGrantScope
      {
        CanReadReports = true,
        CanDownloadReports = true,
        AllowedReportIds = [],
        AllowedReportTypes = []
      },
      Status = AccessGrantStatus.Active,
      StartsAt = now,
      ExpiresAt = expiresAt,
      CreatedAt = now
    };

    await accessGrantRepository.AddAsync(grant, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    await auditLoggingService.LogDataAccessAsync(
      patient,
      "emergency_access.requested",
      "access_grant",
      grant.Id,
      201,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["doctorUserId"] = doctor.Id.ToString("D"),
        ["emergencyContactId"] = contact.Id.ToString("D"),
        ["durationHours"] = durationHours.ToString(System.Globalization.CultureInfo.InvariantCulture)
      },
      cancellationToken);

    await transactionalEmailNotificationService.SendEmergencyAccessRequestedAsync(
      patient,
      contact,
      doctor,
      grant,
      cancellationToken);

    await criticalSmsNotificationService.SendEmergencyAccessRequestedAsync(
      patient,
      contact,
      doctor,
      grant,
      cancellationToken);

    await pushNotificationService.SendToUserAsync(
      patientSub,
      NotificationEventTypes.EmergencyAccess,
      new SendPushNotificationRequest(
        "Emergency Access Requested",
        $"Emergency access was granted to Dr. {doctor.FirstName} {doctor.LastName} until {expiresAt:yyyy-MM-dd HH:mm} UTC.",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["grantId"] = grant.Id.ToString("D"),
          ["contactId"] = contact.Id.ToString("D"),
          ["doctorSub"] = doctorSub
        }),
      cancellationToken);

    return new EmergencyAccessResponse(
      grant.Id,
      patientSub,
      doctorSub,
      contact.Id,
      grant.StartsAt,
      expiresAt,
      grant.GrantReason ?? string.Empty);
  }

  private async Task<User> ResolveUserAsync(
    string userSub,
    UserRole requiredRole,
    string label,
    CancellationToken cancellationToken)
  {
    var user = await userRepository.GetByExternalAuthIdAsync(userSub, cancellationToken)
      ?? throw new InvalidOperationException($"{label} is not provisioned.");
    if (user.Role != requiredRole)
    {
      throw new InvalidOperationException($"{label} must have role {requiredRole}.");
    }

    return user;
  }

  private async Task<EmergencyContact> ResolveEmergencyContactAsync(
    Guid patientId,
    string phone,
    CancellationToken cancellationToken)
  {
    if (!InMemoryPhoneOtpService.TryNormalizeIndianPhone(phone, out var normalizedPhone))
    {
      throw new InvalidOperationException("EmergencyContactPhone must be a valid Indian mobile in +91 format.");
    }

    var contacts = await emergencyContactRepository.ListByUserAsync(patientId, cancellationToken);
    var match = contacts.FirstOrDefault(contact =>
      InMemoryPhoneOtpService.TryNormalizeIndianPhone(contact.Phone, out var normalizedStoredPhone)
      && string.Equals(normalizedStoredPhone, normalizedPhone, StringComparison.Ordinal));
    return match ?? throw new InvalidOperationException("Only registered emergency contacts can request emergency access.");
  }
}
