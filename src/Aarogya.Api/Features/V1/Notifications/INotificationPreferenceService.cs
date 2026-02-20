using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Notifications;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API controller constructor for dependency injection.")]
public interface INotificationPreferenceService
{
  public Task<NotificationPreferencesResponse> GetForUserAsync(
    string userSub,
    CancellationToken cancellationToken = default);

  public Task<NotificationPreferencesResponse> UpdateForUserAsync(
    string userSub,
    UpdateNotificationPreferencesRequest request,
    CancellationToken cancellationToken = default);

  public Task<bool> IsEnabledAsync(
    string userSub,
    string eventType,
    string channel,
    CancellationToken cancellationToken = default);
}
