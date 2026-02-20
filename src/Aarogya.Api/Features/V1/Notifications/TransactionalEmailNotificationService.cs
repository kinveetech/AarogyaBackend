using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class TransactionalEmailNotificationService(
  ITransactionalEmailSender emailSender,
  INotificationPreferenceService preferenceService,
  IOptions<EmailNotificationsOptions> options)
  : ITransactionalEmailNotificationService
{
  private readonly EmailNotificationsOptions _options = options.Value;

  public async Task SendReportUploadedAsync(
    Domain.Entities.User patient,
    Domain.Entities.Report report,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(patient.Email))
    {
      return;
    }

    var userSub = ResolveUserSub(patient);
    if (!await preferenceService.IsEnabledAsync(userSub, NotificationEventTypes.ReportUploaded, NotificationChannels.Email, cancellationToken))
    {
      return;
    }

    var unsubscribeUrl = BuildUnsubscribeUrl(patient.ExternalAuthId, patient.Email);
    var template = TransactionalEmailTemplateBuilder.BuildReportUploaded(patient, report, unsubscribeUrl);
    await emailSender.SendAsync(
      patient.Email,
      $"{patient.FirstName} {patient.LastName}".Trim(),
      template.Subject,
      template.HtmlBody,
      template.TextBody,
      cancellationToken);
  }

  public async Task SendAccessGrantedAsync(
    Domain.Entities.User patient,
    Domain.Entities.User doctor,
    Domain.Entities.AccessGrant grant,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(doctor.Email))
    {
      return;
    }

    var userSub = ResolveUserSub(doctor);
    if (!await preferenceService.IsEnabledAsync(userSub, NotificationEventTypes.AccessGranted, NotificationChannels.Email, cancellationToken))
    {
      return;
    }

    var unsubscribeUrl = BuildUnsubscribeUrl(doctor.ExternalAuthId, doctor.Email);
    var template = TransactionalEmailTemplateBuilder.BuildAccessGranted(doctor, patient, grant, unsubscribeUrl);
    await emailSender.SendAsync(
      doctor.Email,
      $"{doctor.FirstName} {doctor.LastName}".Trim(),
      template.Subject,
      template.HtmlBody,
      template.TextBody,
      cancellationToken);
  }

  public async Task SendEmergencyAccessEventAsync(
    Domain.Entities.User patient,
    Domain.Entities.EmergencyContact contact,
    string action,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(patient.Email))
    {
      return;
    }

    var userSub = ResolveUserSub(patient);
    if (!await preferenceService.IsEnabledAsync(userSub, NotificationEventTypes.EmergencyAccess, NotificationChannels.Email, cancellationToken))
    {
      return;
    }

    var unsubscribeUrl = BuildUnsubscribeUrl(patient.ExternalAuthId, patient.Email);
    var template = TransactionalEmailTemplateBuilder.BuildEmergencyAccessEvent(patient, contact, action, unsubscribeUrl);
    await emailSender.SendAsync(
      patient.Email,
      $"{patient.FirstName} {patient.LastName}".Trim(),
      template.Subject,
      template.HtmlBody,
      template.TextBody,
      cancellationToken);
  }

  public async Task SendEmergencyAccessRequestedAsync(
    Domain.Entities.User patient,
    Domain.Entities.EmergencyContact contact,
    Domain.Entities.User doctor,
    Domain.Entities.AccessGrant grant,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(patient.Email))
    {
      return;
    }

    var userSub = ResolveUserSub(patient);
    if (!await preferenceService.IsEnabledAsync(userSub, NotificationEventTypes.EmergencyAccess, NotificationChannels.Email, cancellationToken))
    {
      return;
    }

    var unsubscribeUrl = BuildUnsubscribeUrl(patient.ExternalAuthId, patient.Email);
    var template = TransactionalEmailTemplateBuilder.BuildEmergencyAccessRequested(patient, contact, doctor, grant, unsubscribeUrl);
    await emailSender.SendAsync(
      patient.Email,
      $"{patient.FirstName} {patient.LastName}".Trim(),
      template.Subject,
      template.HtmlBody,
      template.TextBody,
      cancellationToken);
  }

  private string BuildUnsubscribeUrl(string? userSub, string email)
  {
    var baseUrl = _options.UnsubscribeBaseUrl.TrimEnd('/');
    var identity = string.IsNullOrWhiteSpace(userSub) ? email : userSub;
    return $"{baseUrl}?user={Uri.EscapeDataString(identity)}";
  }

  private static string ResolveUserSub(Domain.Entities.User user)
    => string.IsNullOrWhiteSpace(user.ExternalAuthId) ? user.Id.ToString("D") : user.ExternalAuthId;
}
