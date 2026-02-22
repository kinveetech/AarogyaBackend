using System.Net;
using System.Text.Json;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Notifications;

public sealed class FirebasePushNotificationSenderTests
{
  private static readonly SendPushNotificationRequest SampleRequest =
    new("Test Title", "Test Body");

  [Fact]
  public async Task SendAsync_Should_ReturnZeroCounts_WhenTokenListIsEmptyAsync()
  {
    var sut = CreateSender(enableSending: true);

    var result = await sut.SendAsync([], SampleRequest);

    result.RequestedDeviceCount.Should().Be(0);
    result.SuccessCount.Should().Be(0);
    result.FailureCount.Should().Be(0);
  }

  [Fact]
  public async Task SendAsync_Should_ReturnDisabled_WhenSendingIsOffAsync()
  {
    var sut = CreateSender(enableSending: false);

    var result = await sut.SendAsync(["token-1"], SampleRequest);

    result.SendingEnabled.Should().BeFalse();
    result.RequestedDeviceCount.Should().Be(1);
    result.FailureCount.Should().Be(1);
    result.SuccessCount.Should().Be(0);
  }

  [Fact]
  public async Task SendAsync_Should_ReturnFailure_WhenHttpRequestFailsAsync()
  {
    var handler = CreateMockHandler(HttpStatusCode.InternalServerError, "");
    var sut = CreateSender(enableSending: true, handler: handler);

    var result = await sut.SendAsync(["token-1", "token-2"], SampleRequest);

    result.SendingEnabled.Should().BeTrue();
    result.RequestedDeviceCount.Should().Be(2);
    result.SuccessCount.Should().Be(0);
    result.FailureCount.Should().Be(2);
  }

  [Fact]
  public async Task SendAsync_Should_ReturnSuccess_WhenHttpRequestSucceedsAsync()
  {
    var responseBody = JsonSerializer.Serialize(new { success = 2, failure = 0 });
    var handler = CreateMockHandler(HttpStatusCode.OK, responseBody);
    var sut = CreateSender(enableSending: true, handler: handler);

    var result = await sut.SendAsync(["token-1", "token-2"], SampleRequest);

    result.SendingEnabled.Should().BeTrue();
    result.RequestedDeviceCount.Should().Be(2);
    result.SuccessCount.Should().Be(2);
    result.FailureCount.Should().Be(0);
  }

  [Fact]
  public async Task SendAsync_Should_HandleEmptyResponseBody_AsFullSuccessAsync()
  {
    var handler = CreateMockHandler(HttpStatusCode.OK, "");
    var sut = CreateSender(enableSending: true, handler: handler);

    var result = await sut.SendAsync(["token-1"], SampleRequest);

    result.SuccessCount.Should().Be(1);
    result.FailureCount.Should().Be(0);
  }

  private static FirebasePushNotificationSender CreateSender(
    bool enableSending,
    Mock<HttpMessageHandler>? handler = null)
  {
    handler ??= CreateMockHandler(HttpStatusCode.OK, "{}");
    var httpClient = new HttpClient(handler.Object);

    var options = Options.Create(new FirebaseMessagingOptions
    {
      EnableSending = enableSending,
      ServerKey = enableSending ? "test-server-key" : null,
      Endpoint = "https://fcm.example.com/send"
    });

    return new FirebasePushNotificationSender(
      httpClient,
      options,
      NullLogger<FirebasePushNotificationSender>.Instance);
  }

  private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
  {
    var handler = new Mock<HttpMessageHandler>();
    handler
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync(new HttpResponseMessage(statusCode)
      {
        Content = new StringContent(content)
      });
    return handler;
  }
}
