using Aarogya.Api.Security;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class PushNotificationService(
  IDeviceTokenRegistry tokenRegistry,
  INotificationPreferenceService preferenceService,
  IPushNotificationSender pushNotificationSender)
  : IPushNotificationService
{
  public Task<IReadOnlyList<DeviceTokenRegistrationResponse>> ListRegisteredDevicesAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    return tokenRegistry.ListByUserAsync(normalizedSub, cancellationToken);
  }

  public Task<DeviceTokenRegistrationResponse> RegisterDeviceAsync(
    string userSub,
    RegisterDeviceTokenRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    return RegisterDeviceWithDefaultsAsync(normalizedSub, request, cancellationToken);
  }

  public Task<bool> DeregisterDeviceAsync(
    string userSub,
    string deviceToken,
    CancellationToken cancellationToken = default)
  {
    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    var normalizedToken = InputSanitizer.SanitizePlainText(deviceToken);
    return tokenRegistry.RemoveAsync(normalizedSub, normalizedToken, cancellationToken);
  }

  public Task<NotificationPreferencesResponse> GetPreferencesAsync(
    string userSub,
    CancellationToken cancellationToken = default)
  {
    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    return preferenceService.GetForUserAsync(normalizedSub, cancellationToken);
  }

  public Task<NotificationPreferencesResponse> UpdatePreferencesAsync(
    string userSub,
    UpdateNotificationPreferencesRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    return preferenceService.UpdateForUserAsync(normalizedSub, request, cancellationToken);
  }

  public async Task<PushNotificationDeliveryResponse> SendToCurrentUserAsync(
    string userSub,
    string eventType,
    SendPushNotificationRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
    var normalizedEvent = InputSanitizer.SanitizePlainText(eventType);
    var pushEnabled = await preferenceService.IsEnabledAsync(
      normalizedSub,
      normalizedEvent,
      NotificationChannels.Push,
      cancellationToken);
    if (!pushEnabled)
    {
      return new PushNotificationDeliveryResponse(
        RequestedDeviceCount: 0,
        SuccessCount: 0,
        FailureCount: 0,
        SendingEnabled: false);
    }

    var tokens = await tokenRegistry.GetDeviceTokensAsync(normalizedSub, cancellationToken);
    if (tokens.Count == 0)
    {
      return new PushNotificationDeliveryResponse(
        RequestedDeviceCount: 0,
        SuccessCount: 0,
        FailureCount: 0,
        SendingEnabled: false);
    }

    return await pushNotificationSender.SendAsync(tokens, request, cancellationToken);
  }

  private async Task<DeviceTokenRegistrationResponse> RegisterDeviceWithDefaultsAsync(
    string normalizedSub,
    RegisterDeviceTokenRequest request,
    CancellationToken cancellationToken)
  {
    _ = await preferenceService.GetForUserAsync(normalizedSub, cancellationToken);
    return await tokenRegistry.UpsertAsync(normalizedSub, request, cancellationToken);
  }
}
