using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Notifications;

public sealed class TransactionalEmailTemplateBuilderTests
{
  private const string UnsubscribeUrl = "https://aarogya.app/unsubscribe?token=abc123";

  private static User CreateUser(string first = "Jane", string last = "Doe") =>
    new() { FirstName = first, LastName = last, Email = "jane@example.com" };

  private static Report CreateReport(string reportNumber = "RPT-001") =>
    new() { ReportNumber = reportNumber };

  private static AccessGrant CreateGrant(
    string? reason = "care",
    DateTimeOffset? expiresAt = null) =>
    new()
    {
      GrantReason = reason,
      ExpiresAt = expiresAt ?? new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)
    };

  private static EmergencyContact CreateContact(string name = "John Contact") =>
    new() { Name = name };

  [Fact]
  public void BuildReportUploaded_Should_ProduceNonEmptySubjectHtmlAndText()
  {
    var (subject, html, text) = TransactionalEmailTemplateBuilder.BuildReportUploaded(
      CreateUser(), CreateReport(), UnsubscribeUrl);

    subject.Should().NotBeNullOrWhiteSpace();
    html.Should().NotBeNullOrWhiteSpace();
    text.Should().NotBeNullOrWhiteSpace();
    subject.Should().Contain("RPT-001");
    html.Should().Contain("RPT-001");
    text.Should().Contain("RPT-001");
  }

  [Fact]
  public void BuildAccessGranted_Should_ProduceNonEmptySubjectHtmlAndText()
  {
    var doctor = CreateUser("Dr. Alice", "Smith");
    var patient = CreateUser();
    var grant = CreateGrant();

    var (subject, html, text) = TransactionalEmailTemplateBuilder.BuildAccessGranted(
      doctor, patient, grant, UnsubscribeUrl);

    subject.Should().NotBeNullOrWhiteSpace();
    html.Should().NotBeNullOrWhiteSpace();
    text.Should().NotBeNullOrWhiteSpace();
    subject.Should().Contain("Access Granted");
  }

  [Fact]
  public void BuildEmergencyAccessEvent_Should_ProduceNonEmptySubjectHtmlAndText()
  {
    var (subject, html, text) = TransactionalEmailTemplateBuilder.BuildEmergencyAccessEvent(
      CreateUser(), CreateContact(), "added", UnsubscribeUrl);

    subject.Should().NotBeNullOrWhiteSpace();
    html.Should().NotBeNullOrWhiteSpace();
    text.Should().NotBeNullOrWhiteSpace();
    subject.Should().Contain("added");
  }

  [Fact]
  public void BuildEmergencyAccessRequested_Should_ProduceNonEmptySubjectHtmlAndText()
  {
    var doctor = CreateUser("Dr. Bob", "Jones");
    var (subject, html, text) = TransactionalEmailTemplateBuilder.BuildEmergencyAccessRequested(
      CreateUser(), CreateContact(), doctor, CreateGrant(), UnsubscribeUrl);

    subject.Should().NotBeNullOrWhiteSpace();
    html.Should().NotBeNullOrWhiteSpace();
    text.Should().NotBeNullOrWhiteSpace();
    subject.Should().Contain("Emergency Access Requested");
  }

  [Fact]
  public void BuildEmergencyAccessExpiringSoon_Should_ProduceNonEmptySubjectHtmlAndText()
  {
    var doctor = CreateUser("Dr. Bob", "Jones");
    var (subject, html, text) = TransactionalEmailTemplateBuilder.BuildEmergencyAccessExpiringSoon(
      CreateUser(), doctor, CreateGrant(), UnsubscribeUrl);

    subject.Should().NotBeNullOrWhiteSpace();
    html.Should().NotBeNullOrWhiteSpace();
    text.Should().NotBeNullOrWhiteSpace();
    subject.Should().Contain("Expiring Soon");
  }

  [Fact]
  public void BuildReportUploaded_Should_HtmlEncodeUserInput()
  {
    var user = CreateUser("Jane<script>", "Doe&Co");
    var report = CreateReport("RPT-<img>");

    var (_, html, _) = TransactionalEmailTemplateBuilder.BuildReportUploaded(
      user, report, UnsubscribeUrl);

    html.Should().NotContain("<script>");
    html.Should().NotContain("<img>");
    html.Should().Contain("&lt;script&gt;");
    html.Should().Contain("&lt;img&gt;");
  }

  [Fact]
  public void AllTemplates_Should_IncludeUnsubscribeLink()
  {
    var user = CreateUser();
    var doctor = CreateUser("Dr. A", "B");
    var report = CreateReport();
    var grant = CreateGrant();
    var contact = CreateContact();

    var templates = new[]
    {
      TransactionalEmailTemplateBuilder.BuildReportUploaded(user, report, UnsubscribeUrl),
      TransactionalEmailTemplateBuilder.BuildAccessGranted(doctor, user, grant, UnsubscribeUrl),
      TransactionalEmailTemplateBuilder.BuildEmergencyAccessEvent(user, contact, "added", UnsubscribeUrl),
      TransactionalEmailTemplateBuilder.BuildEmergencyAccessRequested(user, contact, doctor, grant, UnsubscribeUrl),
      TransactionalEmailTemplateBuilder.BuildEmergencyAccessExpiringSoon(user, doctor, grant, UnsubscribeUrl)
    };

    foreach (var (_, html, text) in templates)
    {
      html.Should().Contain("unsubscribe");
      text.Should().Contain(UnsubscribeUrl);
    }
  }
}
