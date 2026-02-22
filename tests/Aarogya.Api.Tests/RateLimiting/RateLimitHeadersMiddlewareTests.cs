using Aarogya.Api.Authentication;
using Aarogya.Api.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.RateLimiting;

public sealed class RateLimitHeadersMiddlewareTests
{
  private static readonly DateTimeOffset T0 = new(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task InvokeAsync_Should_NotWriteHeaders_WhenNoDescriptorPresentAsync()
  {
    var context = new DefaultHttpContext();
    var counter = new Mock<IRateLimitHeaderCounter>();
    var clock = new FakeClock(T0);
    var nextCalled = false;
    var middleware = new RateLimitHeadersMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    }, counter.Object, clock);

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeTrue();
    context.Response.Headers.Should().NotContainKey("X-RateLimit-Limit");
    context.Response.Headers.Should().NotContainKey("X-RateLimit-Remaining");
    context.Response.Headers.Should().NotContainKey("X-RateLimit-Reset");
  }

  [Fact]
  public async Task InvokeAsync_Should_CallCounter_WhenDescriptorIsPresentAsync()
  {
    var context = new DefaultHttpContext();
    var descriptor = new RateLimitDescriptor(
      "test-policy", "user-1", PermitLimit: 100, TimeSpan.FromMinutes(1),
      IsSlidingWindow: false, SegmentsPerWindow: 1);
    context.Items[RateLimitDescriptor.HttpContextItemKey] = descriptor;

    var resetAt = T0.AddMinutes(1);
    var snapshot = new RateLimitHeaderSnapshot(100, 99, resetAt);
    var counter = new Mock<IRateLimitHeaderCounter>();
    counter.Setup(x => x.TrackAccepted(descriptor, T0)).Returns(snapshot);
    var clock = new FakeClock(T0);
    var nextCalled = false;
    var middleware = new RateLimitHeadersMiddleware(_ =>
    {
      nextCalled = true;
      return Task.CompletedTask;
    }, counter.Object, clock);

    await middleware.InvokeAsync(context);

    nextCalled.Should().BeTrue();
    counter.Verify(x => x.TrackAccepted(descriptor, T0), Times.Once);
  }

  [Fact]
  public void ApplyHeaders_Should_SetRateLimitHeadersOnResponseHeaders()
  {
    var headers = new HeaderDictionary();
    var resetAt = T0.AddMinutes(1);
    var snapshot = new RateLimitHeaderSnapshot(100, 99, resetAt);

    RateLimitHeadersMiddleware.ApplyHeaders(headers, snapshot);

    headers["X-RateLimit-Limit"].ToString().Should().Be("100");
    headers["X-RateLimit-Remaining"].ToString().Should().Be("99");
    headers["X-RateLimit-Reset"].ToString()
      .Should().Be(resetAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
