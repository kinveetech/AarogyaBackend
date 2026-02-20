namespace Aarogya.Api.Features.V1.Notifications;

internal interface IPushNotificationSender
{
  public Task<PushNotificationDeliveryResponse> SendAsync(
    IReadOnlyList<string> deviceTokens,
    SendPushNotificationRequest request,
    CancellationToken cancellationToken = default);
}
