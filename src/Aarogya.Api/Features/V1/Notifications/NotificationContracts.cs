using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Aarogya.Api.Features.V1.Notifications;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signature for model binding.")]
public sealed record RegisterDeviceTokenRequest(
  [property: JsonRequired] string DeviceToken,
  [property: JsonRequired] string Platform,
  string? DeviceName = null,
  string? AppVersion = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record DeviceTokenRegistrationResponse(
  Guid RegistrationId,
  string DeviceToken,
  string Platform,
  string? DeviceName,
  string? AppVersion,
  DateTimeOffset RegisteredAt,
  DateTimeOffset UpdatedAt);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record SendPushNotificationRequest(
  [property: JsonRequired] string Title,
  [property: JsonRequired] string Body,
  IReadOnlyDictionary<string, string>? Data = null);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Referenced by public API action signature.")]
public sealed record PushNotificationDeliveryResponse(
  int RequestedDeviceCount,
  int SuccessCount,
  int FailureCount,
  bool SendingEnabled);
