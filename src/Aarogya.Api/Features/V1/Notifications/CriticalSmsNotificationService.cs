using Aarogya.Domain.Entities;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class CriticalSmsNotificationService(
  ISmsSender smsSender,
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

    var message = $"Aarogya alert: emergency contact {contact.Name} was {action}.";
    var result = await smsSender.SendAsync(patient.Phone, message, "emergency_access", cancellationToken);
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
}
