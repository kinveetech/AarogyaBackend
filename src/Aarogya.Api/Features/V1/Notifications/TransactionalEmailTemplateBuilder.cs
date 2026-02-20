using System.Net;
using Aarogya.Domain.Entities;

namespace Aarogya.Api.Features.V1.Notifications;

internal static class TransactionalEmailTemplateBuilder
{
  public static (string Subject, string HtmlBody, string TextBody) BuildReportUploaded(
    User patient,
    Report report,
    string unsubscribeUrl)
  {
    var safeName = WebUtility.HtmlEncode($"{patient.FirstName} {patient.LastName}".Trim());
    var safeReportNumber = WebUtility.HtmlEncode(report.ReportNumber);
    var subject = $"Aarogya: New Report Uploaded ({report.ReportNumber})";
    var html = $$"""
                 <html><body style="font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                 <h2>New Medical Report Uploaded</h2>
                 <p>Hello {{safeName}},</p>
                 <p>Your report <strong>{{safeReportNumber}}</strong> has been uploaded to your Aarogya account.</p>
                 <p>You can review it in the app under Reports.</p>
                 {{BuildUnsubscribeFooter(unsubscribeUrl)}}
                 </body></html>
                 """;
    var text = $"Hello {patient.FirstName},\nYour report {report.ReportNumber} has been uploaded.\nUnsubscribe: {unsubscribeUrl}";
    return (subject, html, text);
  }

  public static (string Subject, string HtmlBody, string TextBody) BuildAccessGranted(
    User doctor,
    User patient,
    AccessGrant grant,
    string unsubscribeUrl)
  {
    var safeDoctorName = WebUtility.HtmlEncode($"{doctor.FirstName} {doctor.LastName}".Trim());
    var safePatientName = WebUtility.HtmlEncode($"{patient.FirstName} {patient.LastName}".Trim());
    var safePurpose = WebUtility.HtmlEncode(grant.GrantReason ?? "care");
    var subject = $"Aarogya: Access Granted by {patient.FirstName} {patient.LastName}";
    var html = $$"""
                 <html><body style="font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                 <h2>Medical Data Access Granted</h2>
                 <p>Hello {{safeDoctorName}},</p>
                 <p>{{safePatientName}} has granted you medical data access for: <strong>{{safePurpose}}</strong>.</p>
                 <p>This access starts immediately and will expire on {{grant.ExpiresAt:yyyy-MM-dd}}.</p>
                 {{BuildUnsubscribeFooter(unsubscribeUrl)}}
                 </body></html>
                 """;
    var text = $"Hello {doctor.FirstName},\n{patient.FirstName} {patient.LastName} granted access for '{grant.GrantReason}'.\nUnsubscribe: {unsubscribeUrl}";
    return (subject, html, text);
  }

  public static (string Subject, string HtmlBody, string TextBody) BuildEmergencyAccessEvent(
    User patient,
    EmergencyContact contact,
    string action,
    string unsubscribeUrl)
  {
    var safePatientName = WebUtility.HtmlEncode($"{patient.FirstName} {patient.LastName}".Trim());
    var safeContactName = WebUtility.HtmlEncode(contact.Name);
    var safeAction = WebUtility.HtmlEncode(action);
    var subject = $"Aarogya: Emergency Contact {action}";
    var html = $$"""
                 <html><body style="font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
                 <h2>Emergency Access Update</h2>
                 <p>Hello {{safePatientName}},</p>
                 <p>Your emergency contact <strong>{{safeContactName}}</strong> was <strong>{{safeAction}}</strong>.</p>
                 <p>If this was not you, please review your account security settings immediately.</p>
                 {{BuildUnsubscribeFooter(unsubscribeUrl)}}
                 </body></html>
                 """;
    var text = $"Hello {patient.FirstName},\nEmergency contact {contact.Name} was {action}.\nUnsubscribe: {unsubscribeUrl}";
    return (subject, html, text);
  }

  private static string BuildUnsubscribeFooter(string unsubscribeUrl)
  {
    var safeUrl = WebUtility.HtmlEncode(unsubscribeUrl);
    return $"""
            <hr style="margin-top:24px;" />
            <p style="font-size:12px;color:#6b7280;">
              To unsubscribe from these notifications, click
              <a href="{safeUrl}" target="_blank" rel="noopener noreferrer">here</a>.
            </p>
            """;
  }
}
