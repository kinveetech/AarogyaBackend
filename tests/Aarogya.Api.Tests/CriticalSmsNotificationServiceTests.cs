using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class CriticalSmsNotificationServiceTests
{
  [Fact]
  public async Task SendEmergencyAccessEventAsync_ShouldSkip_WhenSmsPreferenceDisabledAsync()
  {
    var sender = new FakeSmsSender();
    var preferences = new InMemoryNotificationPreferenceService();
    await preferences.UpdateForUserAsync(
      "seed-PATIENT-IT",
      new UpdateNotificationPreferencesRequest(
        ReportUploaded: new NotificationChannelPreferences(Push: true, Email: true, Sms: true),
        AccessGranted: new NotificationChannelPreferences(Push: true, Email: true, Sms: true),
        EmergencyAccess: new NotificationChannelPreferences(Push: true, Email: true, Sms: false)));

    var service = new CriticalSmsNotificationService(sender, preferences, NullLogger<CriticalSmsNotificationService>.Instance);
    await service.SendEmergencyAccessEventAsync(
      new User
      {
        Id = Guid.NewGuid(),
        ExternalAuthId = "seed-PATIENT-IT",
        Phone = "+919876543210"
      },
      new EmergencyContact
      {
        Id = Guid.NewGuid(),
        Name = "Kin One"
      },
      "created");

    sender.SendCount.Should().Be(0);
  }

  private sealed class FakeSmsSender : ISmsSender
  {
    public int SendCount { get; private set; }

    public Task<SmsSendResult> SendAsync(
      string phoneNumber,
      string message,
      string notificationType,
      CancellationToken cancellationToken = default)
    {
      SendCount++;
      return Task.FromResult(new SmsSendResult(true, false, 0.25m, "mock-1"));
    }
  }
}
