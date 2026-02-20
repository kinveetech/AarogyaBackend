using System.Collections.Concurrent;
using Aarogya.Api.Security;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class InMemoryNotificationPreferenceService : INotificationPreferenceService
{
  private readonly ConcurrentDictionary<string, NotificationPreferencesResponse> _entries = new(StringComparer.Ordinal);

  public Task<NotificationPreferencesResponse> GetForUserAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    return Task.FromResult(GetOrCreate(normalizedSub));
  }

  public Task<NotificationPreferencesResponse> UpdateForUserAsync(
    string userSub,
    UpdateNotificationPreferencesRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    var updated = new NotificationPreferencesResponse(
      request.ReportUploaded,
      request.AccessGranted,
      request.EmergencyAccess);
    _entries[normalizedSub] = updated;
    return Task.FromResult(updated);
  }

  public Task<bool> IsEnabledAsync(
    string userSub,
    string eventType,
    string channel,
    CancellationToken cancellationToken = default)
  {
    var preferences = GetOrCreate(InputSanitizer.SanitizePlainText(userSub));
    var eventPreferences = eventType switch
    {
      NotificationEventTypes.ReportUploaded => preferences.ReportUploaded,
      NotificationEventTypes.AccessGranted => preferences.AccessGranted,
      NotificationEventTypes.EmergencyAccess => preferences.EmergencyAccess,
      _ => new NotificationChannelPreferences(true, true, true)
    };

    var enabled = channel switch
    {
      NotificationChannels.Push => eventPreferences.Push,
      NotificationChannels.Email => eventPreferences.Email,
      NotificationChannels.Sms => eventPreferences.Sms,
      _ => true
    };

    return Task.FromResult(enabled);
  }

  private NotificationPreferencesResponse GetOrCreate(string userSub)
  {
    return _entries.GetOrAdd(
      userSub,
      static _ => new NotificationPreferencesResponse(
        ReportUploaded: new NotificationChannelPreferences(Push: true, Email: true, Sms: true),
        AccessGranted: new NotificationChannelPreferences(Push: true, Email: true, Sms: true),
        EmergencyAccess: new NotificationChannelPreferences(Push: true, Email: true, Sms: true)));
  }
}
