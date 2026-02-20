using Aarogya.Api.Security;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class PushNotificationService(
  IDeviceTokenRegistry tokenRegistry,
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
    return tokenRegistry.UpsertAsync(normalizedSub, request, cancellationToken);
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

  public async Task<PushNotificationDeliveryResponse> SendToCurrentUserAsync(
    string userSub,
    SendPushNotificationRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var normalizedSub = InputSanitizer.SanitizePlainText(userSub);
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
}
