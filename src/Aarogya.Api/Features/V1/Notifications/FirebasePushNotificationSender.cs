using System.Text;
using System.Text.Json;
using Aarogya.Api.Configuration;
using Aarogya.Api.Security;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Notifications;

internal sealed class FirebasePushNotificationSender(
  HttpClient httpClient,
  IOptions<FirebaseMessagingOptions> options,
  ILogger<FirebasePushNotificationSender> logger)
  : IPushNotificationSender
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

  public async Task<PushNotificationDeliveryResponse> SendAsync(
    IReadOnlyList<string> deviceTokens,
    SendPushNotificationRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(deviceTokens);
    ArgumentNullException.ThrowIfNull(request);

    if (deviceTokens.Count == 0)
    {
      return new PushNotificationDeliveryResponse(0, 0, 0, options.Value.EnableSending);
    }

    var settings = options.Value;
    if (!settings.EnableSending || string.IsNullOrWhiteSpace(settings.ServerKey))
    {
      logger.LogInformation(
        "Push notification sending is disabled or not configured. requestedDevices={RequestedDevices}",
        deviceTokens.Count);
      return new PushNotificationDeliveryResponse(deviceTokens.Count, 0, deviceTokens.Count, false);
    }

    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
    _ = requestMessage.Headers.TryAddWithoutValidation("Authorization", $"key={settings.ServerKey}");

    var payload = new
    {
      registration_ids = deviceTokens,
      priority = "high",
      notification = new
      {
        title = InputSanitizer.SanitizePlainText(request.Title),
        body = InputSanitizer.SanitizePlainText(request.Body)
      },
      data = InputSanitizer.SanitizeStringDictionary(request.Data)
    };

    requestMessage.Content = new StringContent(
      JsonSerializer.Serialize(payload, SerializerOptions),
      Encoding.UTF8,
      "application/json");

    using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      logger.LogWarning(
        "Push notification send failed with status {StatusCode}.",
        (int)response.StatusCode);
      return new PushNotificationDeliveryResponse(deviceTokens.Count, 0, deviceTokens.Count, true);
    }

    var raw = await response.Content.ReadAsStringAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(raw))
    {
      return new PushNotificationDeliveryResponse(deviceTokens.Count, deviceTokens.Count, 0, true);
    }

    using var document = JsonDocument.Parse(raw);
    var root = document.RootElement;
    var success = root.TryGetProperty("success", out var successProp) ? successProp.GetInt32() : deviceTokens.Count;
    var failure = root.TryGetProperty("failure", out var failureProp) ? failureProp.GetInt32() : Math.Max(deviceTokens.Count - success, 0);

    return new PushNotificationDeliveryResponse(deviceTokens.Count, success, failure, true);
  }
}
