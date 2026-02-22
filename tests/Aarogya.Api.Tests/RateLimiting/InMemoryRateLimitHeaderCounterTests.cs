using Aarogya.Api.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests.RateLimiting;

public sealed class InMemoryRateLimitHeaderCounterTests
{
  private static readonly DateTimeOffset T0 = new(2026, 2, 20, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public void TrackAccepted_Should_DecrementRemaining_ForFixedWindow()
  {
    var sut = new InMemoryRateLimitHeaderCounter();
    var descriptor = new RateLimitDescriptor(
      "test-policy", "user-1", PermitLimit: 10, TimeSpan.FromMinutes(1),
      IsSlidingWindow: false, SegmentsPerWindow: 1);

    var snap1 = sut.TrackAccepted(descriptor, T0);
    var snap2 = sut.TrackAccepted(descriptor, T0.AddSeconds(5));

    snap1.Limit.Should().Be(10);
    snap1.Remaining.Should().Be(9);
    snap2.Remaining.Should().Be(8);
  }

  [Fact]
  public void TrackAccepted_Should_SetResetTime_ToWindowStartPlusWindow()
  {
    var sut = new InMemoryRateLimitHeaderCounter();
    var window = TimeSpan.FromMinutes(1);
    var descriptor = new RateLimitDescriptor(
      "test-policy", "user-1", PermitLimit: 10, window,
      IsSlidingWindow: false, SegmentsPerWindow: 1);

    var snapshot = sut.TrackAccepted(descriptor, T0);

    snapshot.ResetAt.Should().Be(T0.Add(window));
  }

  [Fact]
  public void TrackAccepted_Should_ResetWindow_WhenWindowExpires()
  {
    var sut = new InMemoryRateLimitHeaderCounter();
    var window = TimeSpan.FromMinutes(1);
    var descriptor = new RateLimitDescriptor(
      "test-policy", "user-1", PermitLimit: 5, window,
      IsSlidingWindow: false, SegmentsPerWindow: 1);

    // Exhaust all permits
    for (var i = 0; i < 5; i++)
    {
      sut.TrackAccepted(descriptor, T0.AddSeconds(i));
    }

    var snapshot = sut.TrackAccepted(descriptor, T0.AddSeconds(5));
    snapshot.Remaining.Should().Be(0);

    // After the window expires, remaining should reset
    var afterWindow = sut.TrackAccepted(descriptor, T0.Add(window));
    afterWindow.Remaining.Should().Be(4);
  }

  [Fact]
  public void TrackAccepted_Should_ClampRemainingToZero_WhenExceeded()
  {
    var sut = new InMemoryRateLimitHeaderCounter();
    var descriptor = new RateLimitDescriptor(
      "test-policy", "user-1", PermitLimit: 1, TimeSpan.FromMinutes(1),
      IsSlidingWindow: false, SegmentsPerWindow: 1);

    sut.TrackAccepted(descriptor, T0);
    var snapshot = sut.TrackAccepted(descriptor, T0.AddSeconds(1));

    snapshot.Remaining.Should().Be(0);
  }

  [Fact]
  public void TrackAccepted_Should_UseSlidingWindow_WhenDescriptorIndicates()
  {
    var sut = new InMemoryRateLimitHeaderCounter();
    var descriptor = new RateLimitDescriptor(
      "test-policy", "user-1", PermitLimit: 3, TimeSpan.FromMinutes(1),
      IsSlidingWindow: true, SegmentsPerWindow: 1);

    var snap1 = sut.TrackAccepted(descriptor, T0);
    snap1.Remaining.Should().Be(2);

    var snap2 = sut.TrackAccepted(descriptor, T0.AddSeconds(10));
    snap2.Remaining.Should().Be(1);

    // After the first hit falls out of the sliding window
    var snap3 = sut.TrackAccepted(descriptor, T0.AddMinutes(1).AddSeconds(1));
    snap3.Remaining.Should().Be(1);
  }
}
