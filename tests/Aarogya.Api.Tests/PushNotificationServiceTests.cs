using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Notifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class PushNotificationServiceTests
{
  [Fact]
  public async Task RegisterDeviceAsync_ShouldPersistAndListRegistrationAsync()
  {
    var registry = new InMemoryDeviceTokenRegistry(new SystemUtcClock());
    var service = new PushNotificationService(registry, new FakePushNotificationSender());

    var registration = await service.RegisterDeviceAsync(
      "seed-PATIENT-IT",
      new RegisterDeviceTokenRequest("token-1", "ios", "iPhone", "1.0.0"));

    var listed = await service.ListRegisteredDevicesAsync("seed-PATIENT-IT");

    registration.DeviceToken.Should().Be("token-1");
    registration.Platform.Should().Be("ios");
    listed.Should().ContainSingle(x => x.DeviceToken == "token-1" && x.Platform == "ios");
  }

  [Fact]
  public async Task DeregisterDeviceAsync_ShouldReturnFalse_WhenTokenMissingAsync()
  {
    var registry = new InMemoryDeviceTokenRegistry(new SystemUtcClock());
    var service = new PushNotificationService(registry, new FakePushNotificationSender());

    var removed = await service.DeregisterDeviceAsync("seed-PATIENT-IT", "missing-token");

    removed.Should().BeFalse();
  }

  [Fact]
  public async Task SendToCurrentUserAsync_ShouldSendToRegisteredTokensAsync()
  {
    var registry = new InMemoryDeviceTokenRegistry(new SystemUtcClock());
    var sender = new FakePushNotificationSender();

    var service = new PushNotificationService(registry, sender);
    await service.RegisterDeviceAsync("seed-PATIENT-IT", new RegisterDeviceTokenRequest("token-1", "ios"));
    await service.RegisterDeviceAsync("seed-PATIENT-IT", new RegisterDeviceTokenRequest("token-2", "android"));

    var result = await service.SendToCurrentUserAsync(
      "seed-PATIENT-IT",
      new SendPushNotificationRequest("Lab Report Ready", "Your report is now available."));

    result.SuccessCount.Should().Be(2);
    sender.LastTokens.Should().Contain(["token-1", "token-2"]);
    sender.InvocationCount.Should().Be(1);
  }

  private sealed class FakePushNotificationSender : IPushNotificationSender
  {
    public int InvocationCount { get; private set; }

    public IReadOnlyList<string> LastTokens { get; private set; } = [];

    public Task<PushNotificationDeliveryResponse> SendAsync(
      IReadOnlyList<string> deviceTokens,
      SendPushNotificationRequest request,
      CancellationToken cancellationToken = default)
    {
      InvocationCount++;
      LastTokens = deviceTokens.ToArray();
      return Task.FromResult(new PushNotificationDeliveryResponse(deviceTokens.Count, deviceTokens.Count, 0, true));
    }
  }
}
