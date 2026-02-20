using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Api.Features.V1.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class MockSmsSenderTests
{
  [Fact]
  public async Task SendAsync_ShouldSucceed_WhenSmsDisabledAsync()
  {
    var sender = CreateSender(
      new SmsNotificationsOptions
      {
        EnableCriticalSms = false
      });

    var result = await sender.SendAsync("+919876543210", "test", "otp");

    result.Success.Should().BeTrue();
    result.IsRateLimited.Should().BeFalse();
    result.EstimatedCostInInr.Should().Be(0m);
  }

  [Fact]
  public async Task SendAsync_ShouldRateLimit_WhenWindowExceededAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero));
    var sender = CreateSender(
      new SmsNotificationsOptions
      {
        EnableCriticalSms = true,
        MaxSendsPerWindow = 2,
        RateLimitWindowSeconds = 60,
        EstimatedCostPerMessageInInr = 0.32m
      },
      clock);

    (await sender.SendAsync("+919876543210", "first", "otp")).Success.Should().BeTrue();
    (await sender.SendAsync("+919876543210", "second", "otp")).Success.Should().BeTrue();

    var third = await sender.SendAsync("+919876543210", "third", "otp");
    third.Success.Should().BeFalse();
    third.IsRateLimited.Should().BeTrue();
  }

  private static MockSmsSender CreateSender(SmsNotificationsOptions options, IUtcClock? clock = null)
  {
    return new MockSmsSender(
      Options.Create(options),
      clock ?? new FakeClock(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)),
      NullLogger<MockSmsSender>.Instance);
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
  }
}
