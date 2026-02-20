using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class InMemoryPhoneOtpServiceTests
{
  [Theory]
  [InlineData("+919876543210")]
  [InlineData("+916123456789")]
  public void TryNormalizeIndianPhone_ShouldAcceptValidIndianMobile(string input)
  {
    var success = InMemoryPhoneOtpService.TryNormalizeIndianPhone(input, out var normalized);

    success.Should().BeTrue();
    normalized.Should().Be(input);
  }

  [Theory]
  [InlineData("9876543210")]
  [InlineData("+915123456789")]
  [InlineData("+91987654321")]
  [InlineData("+1 5551234567")]
  public void TryNormalizeIndianPhone_ShouldRejectInvalidPhone(string input)
  {
    var success = InMemoryPhoneOtpService.TryNormalizeIndianPhone(input, out _);

    success.Should().BeFalse();
  }

  [Fact]
  public async Task RequestOtpAsync_ShouldRateLimit_WhenRequestWindowExceededAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 19, 0, 0, 0, TimeSpan.Zero));
    var sender = new FakePhoneOtpSender();
    var service = CreateService(clock, sender, new OtpOptions
    {
      CodeLength = 6,
      CodeExpirySeconds = 300,
      MaxRequestsPerWindow = 2,
      RateLimitWindowSeconds = 600
    });

    (await service.RequestOtpAsync("+919876543210")).Success.Should().BeTrue();
    (await service.RequestOtpAsync("+919876543210")).Success.Should().BeTrue();
    var rateLimited = await service.RequestOtpAsync("+919876543210");

    rateLimited.Success.Should().BeFalse();
    rateLimited.IsRateLimited.Should().BeTrue();
    sender.Messages.Should().HaveCount(2);
  }

  [Fact]
  public async Task RequestOtpAsync_ShouldReturnRateLimited_WhenSmsSenderRateLimitsAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 19, 0, 0, 0, TimeSpan.Zero));
    var sender = new FakePhoneOtpSender
    {
      ForceRateLimit = true
    };
    var service = CreateService(clock, sender);

    var result = await service.RequestOtpAsync("+919876543210");

    result.Success.Should().BeFalse();
    result.IsRateLimited.Should().BeTrue();
    result.Message.Should().Contain("Too many OTP SMS requests");
  }

  [Fact]
  public async Task VerifyOtpAsync_ShouldSucceed_WithinExpiryWindowAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 19, 0, 0, 0, TimeSpan.Zero));
    var sender = new FakePhoneOtpSender();
    var service = CreateService(clock, sender);

    var requestResult = await service.RequestOtpAsync("+919876543210");
    requestResult.Success.Should().BeTrue();
    var otp = sender.Messages.Single().Otp;

    var verifyResult = await service.VerifyOtpAsync("+919876543210", otp);

    verifyResult.Success.Should().BeTrue();
  }

  [Fact]
  public async Task VerifyOtpAsync_ShouldFail_WhenExpiredAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 19, 0, 0, 0, TimeSpan.Zero));
    var sender = new FakePhoneOtpSender();
    var service = CreateService(clock, sender, new OtpOptions
    {
      CodeLength = 6,
      CodeExpirySeconds = 60,
      MaxRequestsPerWindow = 3,
      RateLimitWindowSeconds = 600
    });

    await service.RequestOtpAsync("+919876543210");
    var otp = sender.Messages.Single().Otp;
    clock.Advance(TimeSpan.FromMinutes(2));

    var verifyResult = await service.VerifyOtpAsync("+919876543210", otp);

    verifyResult.Success.Should().BeFalse();
    verifyResult.Message.Should().MatchRegex("(?i).*expired.*");
  }

  private static InMemoryPhoneOtpService CreateService(FakeClock clock, FakePhoneOtpSender sender, OtpOptions? options = null)
  {
    return new InMemoryPhoneOtpService(
      Options.Create(options ?? new OtpOptions()),
      sender,
      clock);
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Advance(TimeSpan by)
    {
      UtcNow = UtcNow.Add(by);
    }
  }

  private sealed class FakePhoneOtpSender : IPhoneOtpSender
  {
    public List<(string PhoneNumber, string Otp, DateTimeOffset ExpiresAt)> Messages { get; } = [];
    public bool ForceRateLimit { get; set; }

    public Task<OtpDispatchResult> SendOtpAsync(
      string phoneNumber,
      string otp,
      DateTimeOffset expiresAt,
      CancellationToken cancellationToken = default)
    {
      if (ForceRateLimit)
      {
        return Task.FromResult(new OtpDispatchResult(false, true, "Too many OTP SMS requests. Please try again later."));
      }

      Messages.Add((phoneNumber, otp, expiresAt));
      return Task.FromResult(new OtpDispatchResult(true, false, "OTP sent successfully."));
    }
  }
}
