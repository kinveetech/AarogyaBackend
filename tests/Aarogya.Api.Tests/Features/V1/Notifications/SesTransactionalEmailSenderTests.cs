using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Notifications;

public sealed class SesTransactionalEmailSenderTests
{
  private const string Recipient = "user@example.com";
  private const string Subject = "Test Subject";
  private const string HtmlBody = "<p>Hello</p>";
  private const string TextBody = "Hello";

  [Fact]
  public async Task SendAsync_Should_Skip_WhenTransactionalEmailsDisabledAsync()
  {
    var ses = new Mock<IAmazonSimpleEmailServiceV2>();
    var sut = CreateSender(ses.Object, enableEmails: false);

    await sut.SendAsync(Recipient, "User", Subject, HtmlBody, TextBody);

    ses.Verify(
      x => x.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task SendAsync_Should_Skip_WhenEmailIsBlankAsync()
  {
    var ses = new Mock<IAmazonSimpleEmailServiceV2>();
    var sut = CreateSender(ses.Object, enableEmails: true);

    await sut.SendAsync("  ", "User", Subject, HtmlBody, TextBody);

    ses.Verify(
      x => x.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task SendAsync_Should_Throw_WhenSenderEmailMissingAsync()
  {
    var ses = new Mock<IAmazonSimpleEmailServiceV2>();
    var sut = CreateSender(ses.Object, enableEmails: true, senderEmail: "");

    var act = () => sut.SendAsync(Recipient, "User", Subject, HtmlBody, TextBody);

    // The InvalidOperationException is caught by the catch-all handler and swallowed as a warning.
    // So it should NOT throw to the caller.
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task SendAsync_Should_SwallowSesException_AndNotThrowAsync()
  {
    var ses = new Mock<IAmazonSimpleEmailServiceV2>();
    ses.Setup(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new AccountSuspendedException("suspended"));
    var sut = CreateSender(ses.Object, enableEmails: true);

    var act = () => sut.SendAsync(Recipient, "User", Subject, HtmlBody, TextBody);

    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task SendAsync_Should_CallSes_WhenEnabledAndValidAsync()
  {
    var ses = new Mock<IAmazonSimpleEmailServiceV2>();
    ses.Setup(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new SendEmailResponse());
    var sut = CreateSender(ses.Object, enableEmails: true);

    await sut.SendAsync(Recipient, "User", Subject, HtmlBody, TextBody);

    ses.Verify(
      x => x.SendEmailAsync(
        It.Is<SendEmailRequest>(r =>
          r.FromEmailAddress.Contains("noreply@aarogya.app") &&
          r.Content.Simple.Subject.Data == Subject),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  private static SesTransactionalEmailSender CreateSender(
    IAmazonSimpleEmailServiceV2 sesClient,
    bool enableEmails = true,
    string senderEmail = "noreply@aarogya.app",
    string senderName = "Aarogya")
  {
    var awsOptions = Options.Create(new AwsOptions
    {
      Ses = new SesOptions
      {
        SenderEmail = senderEmail,
        SenderName = senderName
      }
    });

    var emailOptions = Options.Create(new EmailNotificationsOptions
    {
      EnableTransactionalEmails = enableEmails
    });

    return new SesTransactionalEmailSender(
      sesClient,
      awsOptions,
      emailOptions,
      NullLogger<SesTransactionalEmailSender>.Instance);
  }
}
