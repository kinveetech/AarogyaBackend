using Aarogya.Api.Features.V1.Notifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class NotificationPreferenceServiceTests
{
  [Fact]
  public async Task GetForUserAsync_ShouldReturnEnabledDefaultsAsync()
  {
    var service = new InMemoryNotificationPreferenceService();

    var preferences = await service.GetForUserAsync("seed-PATIENT-IT");

    preferences.ReportUploaded.Should().Be(new NotificationChannelPreferences(true, true, true));
    preferences.AccessGranted.Should().Be(new NotificationChannelPreferences(true, true, true));
    preferences.EmergencyAccess.Should().Be(new NotificationChannelPreferences(true, true, true));
  }

  [Fact]
  public async Task IsEnabledAsync_ShouldReflectUpdatedPreferencesAsync()
  {
    var service = new InMemoryNotificationPreferenceService();
    await service.UpdateForUserAsync(
      "seed-PATIENT-IT",
      new UpdateNotificationPreferencesRequest(
        ReportUploaded: new NotificationChannelPreferences(Push: false, Email: true, Sms: false),
        AccessGranted: new NotificationChannelPreferences(Push: true, Email: false, Sms: true),
        EmergencyAccess: new NotificationChannelPreferences(Push: true, Email: true, Sms: false)));

    (await service.IsEnabledAsync("seed-PATIENT-IT", NotificationEventTypes.ReportUploaded, NotificationChannels.Push)).Should().BeFalse();
    (await service.IsEnabledAsync("seed-PATIENT-IT", NotificationEventTypes.ReportUploaded, NotificationChannels.Email)).Should().BeTrue();
    (await service.IsEnabledAsync("seed-PATIENT-IT", NotificationEventTypes.EmergencyAccess, NotificationChannels.Sms)).Should().BeFalse();
  }
}
