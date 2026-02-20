using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class TransactionalEmailNotificationServiceTests
{
  [Fact]
  public async Task SendReportUploadedAsync_ShouldSkip_WhenEmailPreferenceDisabledAsync()
  {
    var sender = new FakeTransactionalEmailSender();
    var preferences = new InMemoryNotificationPreferenceService();
    await preferences.UpdateForUserAsync(
      "seed-PATIENT-IT",
      new UpdateNotificationPreferencesRequest(
        ReportUploaded: new NotificationChannelPreferences(Push: true, Email: false, Sms: true),
        AccessGranted: new NotificationChannelPreferences(Push: true, Email: true, Sms: true),
        EmergencyAccess: new NotificationChannelPreferences(Push: true, Email: true, Sms: true)));

    var service = new TransactionalEmailNotificationService(
      sender,
      preferences,
      Options.Create(new EmailNotificationsOptions
      {
        EnableTransactionalEmails = true,
        UnsubscribeBaseUrl = "https://aarogya.app/unsubscribe"
      }));

    await service.SendReportUploadedAsync(
      new User
      {
        Id = Guid.NewGuid(),
        ExternalAuthId = "seed-PATIENT-IT",
        FirstName = "Test",
        LastName = "User",
        Email = "patient@example.com"
      },
      new Report
      {
        Id = Guid.NewGuid(),
        ReportNumber = "REP-123"
      });

    sender.SendCount.Should().Be(0);
  }

  private sealed class FakeTransactionalEmailSender : ITransactionalEmailSender
  {
    public int SendCount { get; private set; }

    public Task SendAsync(
      string toEmail,
      string? toName,
      string subject,
      string htmlBody,
      string textBody,
      CancellationToken cancellationToken = default)
    {
      SendCount++;
      return Task.CompletedTask;
    }
  }
}
