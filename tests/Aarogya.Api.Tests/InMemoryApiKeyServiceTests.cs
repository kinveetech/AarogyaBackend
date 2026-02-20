using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class InMemoryApiKeyServiceTests
{
  [Fact]
  public async Task IssueAndValidateKeyAsync_ShouldSucceed_ForIssuedKeyAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock);

    var issued = await service.IssueKeyAsync(new ApiKeyIssueRequest("lab-1", "Acme Labs"));
    var validated = await service.ValidateKeyAsync(issued.ApiKey!);

    issued.Success.Should().BeTrue();
    validated.Success.Should().BeTrue();
    validated.PartnerId.Should().Be("lab-1");
    validated.PartnerName.Should().Be("Acme Labs");
  }

  [Fact]
  public async Task ValidateKeyAsync_ShouldRateLimit_PerKeyAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock, new ApiKeyOptions
    {
      KeyPrefix = "aarogya_lab_test_",
      MaxRequestsPerWindow = 2,
      RateLimitWindowSeconds = 60,
      DefaultKeyLifetimeDays = 365,
      RotationOverlapMinutes = 60
    });

    var issued = await service.IssueKeyAsync(new ApiKeyIssueRequest("lab-2", "Rate Limit Lab"));

    var first = await service.ValidateKeyAsync(issued.ApiKey!);
    var second = await service.ValidateKeyAsync(issued.ApiKey!);
    var third = await service.ValidateKeyAsync(issued.ApiKey!);

    first.Success.Should().BeTrue();
    second.Success.Should().BeTrue();
    third.Success.Should().BeFalse();
    third.IsRateLimited.Should().BeTrue();
  }

  [Fact]
  public async Task RotateKeyAsync_ShouldKeepPreviousKeyValid_DuringOverlapAsync()
  {
    var clock = new FakeClock(new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var service = CreateService(clock, new ApiKeyOptions
    {
      KeyPrefix = "aarogya_lab_test_",
      MaxRequestsPerWindow = 100,
      RateLimitWindowSeconds = 60,
      DefaultKeyLifetimeDays = 365,
      RotationOverlapMinutes = 30
    });

    var issued = await service.IssueKeyAsync(new ApiKeyIssueRequest("lab-3", "Rotate Lab"));
    var rotated = await service.RotateKeyAsync(new ApiKeyRotateRequest(issued.KeyId!));

    var oldKeyValidation = await service.ValidateKeyAsync(issued.ApiKey!);
    var newKeyValidation = await service.ValidateKeyAsync(rotated.ApiKey!);

    rotated.Success.Should().BeTrue();
    rotated.PreviousKeyValidUntil.Should().NotBeNull();
    oldKeyValidation.Success.Should().BeTrue();
    newKeyValidation.Success.Should().BeTrue();
  }

  private static InMemoryApiKeyService CreateService(FakeClock clock, ApiKeyOptions? options = null)
  {
    return new InMemoryApiKeyService(
      Options.Create(options ?? new ApiKeyOptions
      {
        KeyPrefix = "aarogya_lab_test_",
        MaxRequestsPerWindow = 120,
        RateLimitWindowSeconds = 60,
        DefaultKeyLifetimeDays = 365,
        RotationOverlapMinutes = 1440
      }),
      clock);
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
  }
}
