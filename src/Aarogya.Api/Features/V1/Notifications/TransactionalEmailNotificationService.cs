using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class TransactionalEmailNotificationService(
  ITransactionalEmailSender emailSender,
  IOptions<EmailNotificationsOptions> options)
  : ITransactionalEmailNotificationService
{
  private readonly EmailNotificationsOptions _options = options.Value;

  public Task SendReportUploadedAsync(
    Domain.Entities.User patient,
    Domain.Entities.Report report,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(patient.Email))
    {
      return Task.CompletedTask;
    }

    var unsubscribeUrl = BuildUnsubscribeUrl(patient.ExternalAuthId, patient.Email);
    var template = TransactionalEmailTemplateBuilder.BuildReportUploaded(patient, report, unsubscribeUrl);
    return emailSender.SendAsync(
      patient.Email,
      $"{patient.FirstName} {patient.LastName}".Trim(),
      template.Subject,
      template.HtmlBody,
      template.TextBody,
      cancellationToken);
  }

  public Task SendAccessGrantedAsync(
    Domain.Entities.User patient,
    Domain.Entities.User doctor,
    Domain.Entities.AccessGrant grant,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(doctor.Email))
    {
      return Task.CompletedTask;
    }

    var unsubscribeUrl = BuildUnsubscribeUrl(doctor.ExternalAuthId, doctor.Email);
    var template = TransactionalEmailTemplateBuilder.BuildAccessGranted(doctor, patient, grant, unsubscribeUrl);
    return emailSender.SendAsync(
      doctor.Email,
      $"{doctor.FirstName} {doctor.LastName}".Trim(),
      template.Subject,
      template.HtmlBody,
      template.TextBody,
      cancellationToken);
  }

  public Task SendEmergencyAccessEventAsync(
    Domain.Entities.User patient,
    Domain.Entities.EmergencyContact contact,
    string action,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(patient.Email))
    {
      return Task.CompletedTask;
    }

    var unsubscribeUrl = BuildUnsubscribeUrl(patient.ExternalAuthId, patient.Email);
    var template = TransactionalEmailTemplateBuilder.BuildEmergencyAccessEvent(patient, contact, action, unsubscribeUrl);
    return emailSender.SendAsync(
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
}
