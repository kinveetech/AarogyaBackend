using System.Diagnostics.CodeAnalysis;

namespace Aarogya.Api.Features.V1.Notifications;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API controller constructor for dependency injection.")]
public interface IPushNotificationService
{
  public Task<IReadOnlyList<DeviceTokenRegistrationResponse>> ListRegisteredDevicesAsync(
    string userSub,
    CancellationToken cancellationToken = default);

  public Task<DeviceTokenRegistrationResponse> RegisterDeviceAsync(
    string userSub,
    RegisterDeviceTokenRequest request,
    CancellationToken cancellationToken = default);

  public Task<bool> DeregisterDeviceAsync(
    string userSub,
    string deviceToken,
    CancellationToken cancellationToken = default);

  public Task<NotificationPreferencesResponse> GetPreferencesAsync(
    string userSub,
    CancellationToken cancellationToken = default);

  public Task<NotificationPreferencesResponse> UpdatePreferencesAsync(
    string userSub,
    UpdateNotificationPreferencesRequest request,
    CancellationToken cancellationToken = default);

  public Task<PushNotificationDeliveryResponse> SendToCurrentUserAsync(
    string userSub,
    string eventType,
    SendPushNotificationRequest request,
    CancellationToken cancellationToken = default);

  public Task<PushNotificationDeliveryResponse> SendToUserAsync(
    string userSub,
    string eventType,
    SendPushNotificationRequest request,
    CancellationToken cancellationToken = default);
}
