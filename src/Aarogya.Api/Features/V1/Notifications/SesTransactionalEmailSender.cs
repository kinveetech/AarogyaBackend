using Aarogya.Api.Configuration;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class SesTransactionalEmailSender(
  IAmazonSimpleEmailServiceV2 sesClient,
  IOptions<AwsOptions> awsOptions,
  IOptions<EmailNotificationsOptions> emailOptions,
  ILogger<SesTransactionalEmailSender> logger)
  : ITransactionalEmailSender
{
  private readonly AwsOptions _aws = awsOptions.Value;
  private readonly EmailNotificationsOptions _email = emailOptions.Value;

  public async Task SendAsync(
    string toEmail,
    string? toName,
    string subject,
    string htmlBody,
    string textBody,
    CancellationToken cancellationToken = default)
  {
    if (!_email.EnableTransactionalEmails)
    {
      logger.LogInformation("Transactional email disabled. Skipping email for {Recipient}.", toEmail);
      return;
    }

    if (string.IsNullOrWhiteSpace(toEmail))
    {
      return;
    }

    try
    {
      var displayName = string.IsNullOrWhiteSpace(toName) ? toEmail : $"{toName} <{toEmail}>";
      var senderName = string.IsNullOrWhiteSpace(_aws.Ses.SenderName) ? "Aarogya" : _aws.Ses.SenderName.Trim();
      var sender = string.IsNullOrWhiteSpace(_aws.Ses.SenderEmail)
        ? throw new InvalidOperationException("Aws:Ses:SenderEmail is required for transactional emails.")
        : $"{senderName} <{_aws.Ses.SenderEmail.Trim()}>";

      var request = new SendEmailRequest
      {
        FromEmailAddress = sender,
        Destination = new Destination
        {
          ToAddresses = [displayName]
        },
        Content = new EmailContent
        {
          Simple = new Message
          {
            Subject = new Content { Data = subject, Charset = "UTF-8" },
            Body = new Body
            {
              Html = new Content { Data = htmlBody, Charset = "UTF-8" },
              Text = new Content { Data = textBody, Charset = "UTF-8" }
            }
          }
        }
      };

      _ = await sesClient.SendEmailAsync(request, cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Failed to send transactional email to {Recipient}.", toEmail);
    }
  }
}
