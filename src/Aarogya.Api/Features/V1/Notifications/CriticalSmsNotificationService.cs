using Aarogya.Domain.Entities;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class CriticalSmsNotificationService(
  ISmsSender smsSender,
  INotificationPreferenceService preferenceService,
  ILogger<CriticalSmsNotificationService> logger)
  : ICriticalSmsNotificationService
{
  public async Task SendEmergencyAccessEventAsync(
    User patient,
    EmergencyContact contact,
    string action,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(patient.Phone))
    {
      return;
    }

    var userSub = string.IsNullOrWhiteSpace(patient.ExternalAuthId) ? patient.Id.ToString("D") : patient.ExternalAuthId;
    if (!await preferenceService.IsEnabledAsync(userSub, NotificationEventTypes.EmergencyAccess, NotificationChannels.Sms, cancellationToken))
    {
      return;
    }

    var message = $"Aarogya alert: emergency contact {contact.Name} was {action}.";
    var result = await smsSender.SendAsync(patient.Phone, message, NotificationEventTypes.EmergencyAccess, cancellationToken);
    if (!result.Success)
    {
      logger.LogWarning(
        "Critical SMS for emergency access event was not sent. patientId={PatientId}, contactId={ContactId}, action={Action}, rateLimited={IsRateLimited}",
        patient.Id,
        contact.Id,
        action,
        result.IsRateLimited);
    }
  }

  public async Task SendEmergencyAccessRequestedAsync(
    User patient,
    EmergencyContact contact,
    User doctor,
    AccessGrant grant,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(patient.Phone))
    {
      return;
    }

    var userSub = string.IsNullOrWhiteSpace(patient.ExternalAuthId) ? patient.Id.ToString("D") : patient.ExternalAuthId;
    if (!await preferenceService.IsEnabledAsync(userSub, NotificationEventTypes.EmergencyAccess, NotificationChannels.Sms, cancellationToken))
    {
      return;
    }

    var message = $"Aarogya alert: {contact.Name} requested emergency access for Dr. {doctor.FirstName} {doctor.LastName} until {grant.ExpiresAt:yyyy-MM-dd HH:mm} UTC.";
    var result = await smsSender.SendAsync(patient.Phone, message, NotificationEventTypes.EmergencyAccess, cancellationToken);
    if (!result.Success)
    {
      logger.LogWarning(
        "Critical SMS for emergency access request was not sent. patientId={PatientId}, contactId={ContactId}, doctorId={DoctorId}, rateLimited={IsRateLimited}",
        patient.Id,
        contact.Id,
        doctor.Id,
        result.IsRateLimited);
    }
  }
}
