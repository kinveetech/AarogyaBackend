namespace Aarogya.Api.Features.V1.Notifications;

internal static class NotificationEventTypes
{
  public const string ReportUploaded = "report_uploaded";
  public const string AccessGranted = "access_granted";
  public const string EmergencyAccess = "emergency_access";
}

internal static class NotificationChannels
{
  public const string Push = "push";
  public const string Email = "email";
  public const string Sms = "sms";
}
