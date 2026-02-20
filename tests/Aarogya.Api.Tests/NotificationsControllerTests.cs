using System.Security.Claims;
using Aarogya.Api.Controllers.V1;
using Aarogya.Api.Features.V1.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class NotificationsControllerTests
{
  [Fact]
  public async Task RegisterDeviceAsync_ShouldReturnUnauthorized_WhenSubjectMissingAsync()
  {
    var controller = CreateController(Mock.Of<IPushNotificationService>(), new ClaimsPrincipal(new ClaimsIdentity()));

    var result = await controller.RegisterDeviceAsync(
      new RegisterDeviceTokenRequest("token", "ios"),
      CancellationToken.None);

    result.Should().BeOfType<UnauthorizedResult>();
  }

  [Fact]
  public async Task RegisterDeviceAsync_ShouldReturnCreated_WhenValidAsync()
  {
    var service = new Mock<IPushNotificationService>();
    var response = new DeviceTokenRegistrationResponse(
      Guid.NewGuid(),
      "token-1",
      "ios",
      "iPhone",
      "1.0.0",
      DateTimeOffset.UtcNow,
      DateTimeOffset.UtcNow);
    service
      .Setup(x => x.RegisterDeviceAsync("seed-PATIENT-IT", It.IsAny<RegisterDeviceTokenRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(service.Object, CreateUser("seed-PATIENT-IT"));

    var result = await controller.RegisterDeviceAsync(
      new RegisterDeviceTokenRequest("token-1", "ios", "iPhone", "1.0.0"),
      CancellationToken.None);

    var created = result.Should().BeOfType<CreatedResult>().Subject;
    created.Value.Should().BeEquivalentTo(response);
  }

  [Fact]
  public async Task SendTestNotificationAsync_ShouldReturnOk_WhenUserAuthenticatedAsync()
  {
    var service = new Mock<IPushNotificationService>();
    var response = new PushNotificationDeliveryResponse(1, 1, 0, true);
    service
      .Setup(x => x.SendToCurrentUserAsync("seed-PATIENT-IT", It.IsAny<SendPushNotificationRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var controller = CreateController(service.Object, CreateUser("seed-PATIENT-IT"));

    var result = await controller.SendTestNotificationAsync(
      new SendPushNotificationRequest("Test", "Body"),
      CancellationToken.None);

    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeEquivalentTo(response);
  }

  private static NotificationsController CreateController(IPushNotificationService service, ClaimsPrincipal user)
  {
    return new NotificationsController(service)
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext
        {
          User = user
        }
      }
    };
  }

  private static ClaimsPrincipal CreateUser(string sub)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim("sub", sub)
    ], "TestAuth"));
  }
}
