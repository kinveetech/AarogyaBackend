namespace Aarogya.Api.Features.V1.Notifications;

internal interface ITransactionalEmailSender
{
  public Task SendAsync(
    string toEmail,
    string? toName,
    string subject,
    string htmlBody,
    string textBody,
    CancellationToken cancellationToken = default);
}
