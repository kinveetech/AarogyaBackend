using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Aarogya.Api.Features.V1.Notifications;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signatures.")]
public sealed record NotificationChannelPreferences(
  [property: JsonRequired] bool Push,
  [property: JsonRequired] bool Email,
  [property: JsonRequired] bool Sms);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signatures.")]
public sealed record NotificationPreferencesResponse(
  NotificationChannelPreferences ReportUploaded,
  NotificationChannelPreferences AccessGranted,
  NotificationChannelPreferences EmergencyAccess);

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Used by public API action signatures.")]
public sealed record UpdateNotificationPreferencesRequest(
  [property: JsonRequired] NotificationChannelPreferences ReportUploaded,
  [property: JsonRequired] NotificationChannelPreferences AccessGranted,
  [property: JsonRequired] NotificationChannelPreferences EmergencyAccess);
