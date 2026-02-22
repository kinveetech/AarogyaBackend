using Aarogya.Api.Authentication;
using Aarogya.Api.Features.V1.Notifications;
using FluentAssertions;
using Xunit;

namespace Aarogya.Api.Tests.Features.V1.Notifications;

public sealed class InMemoryDeviceTokenRegistryTests
{
  private static readonly DateTimeOffset T0 = new(2026, 2, 20, 10, 0, 0, TimeSpan.Zero);
  private const string UserSub = "user-sub-1";

  [Fact]
  public async Task UpsertAsync_Should_RegisterNewToken_AndRetrieveViaListAsync()
  {
    var clock = new FakeClock(T0);
    var sut = new InMemoryDeviceTokenRegistry(clock);
    var request = new RegisterDeviceTokenRequest("token-1", "android", "Pixel 9", "1.0.0");

    var result = await sut.UpsertAsync(UserSub, request);

    result.DeviceToken.Should().Be("token-1");
    result.Platform.Should().Be("android");
    result.DeviceName.Should().Be("Pixel 9");
    result.AppVersion.Should().Be("1.0.0");
    result.RegisteredAt.Should().Be(T0);
    result.UpdatedAt.Should().Be(T0);
    result.RegistrationId.Should().NotBeEmpty();

    var list = await sut.ListByUserAsync(UserSub);
    list.Should().ContainSingle().Which.DeviceToken.Should().Be("token-1");
  }

  [Fact]
  public async Task UpsertAsync_Should_UpdateExistingToken_WhenSameDeviceTokenAsync()
  {
    var clock = new FakeClock(T0);
    var sut = new InMemoryDeviceTokenRegistry(clock);
    var request1 = new RegisterDeviceTokenRequest("token-1", "android", "Pixel 8", "1.0.0");
    var first = await sut.UpsertAsync(UserSub, request1);

    clock.Advance(TimeSpan.FromMinutes(5));
    var request2 = new RegisterDeviceTokenRequest("token-1", "android", "Pixel 9", "2.0.0");
    var second = await sut.UpsertAsync(UserSub, request2);

    second.RegistrationId.Should().Be(first.RegistrationId);
    second.DeviceName.Should().Be("Pixel 9");
    second.AppVersion.Should().Be("2.0.0");
    second.RegisteredAt.Should().Be(T0);
    second.UpdatedAt.Should().Be(T0.AddMinutes(5));

    var list = await sut.ListByUserAsync(UserSub);
    list.Should().ContainSingle();
  }

  [Fact]
  public async Task RemoveAsync_Should_ReturnTrue_AndRemoveTokenAsync()
  {
    var clock = new FakeClock(T0);
    var sut = new InMemoryDeviceTokenRegistry(clock);
    await sut.UpsertAsync(UserSub, new RegisterDeviceTokenRequest("token-1", "ios"));

    var removed = await sut.RemoveAsync(UserSub, "token-1");

    removed.Should().BeTrue();
    var list = await sut.ListByUserAsync(UserSub);
    list.Should().BeEmpty();
  }

  [Fact]
  public async Task RemoveAsync_Should_ReturnFalse_WhenTokenDoesNotExistAsync()
  {
    var clock = new FakeClock(T0);
    var sut = new InMemoryDeviceTokenRegistry(clock);

    var removed = await sut.RemoveAsync(UserSub, "nonexistent");

    removed.Should().BeFalse();
  }

  [Fact]
  public async Task ListByUserAsync_Should_ReturnSortedByUpdatedAtDescendingAsync()
  {
    var clock = new FakeClock(T0);
    var sut = new InMemoryDeviceTokenRegistry(clock);

    await sut.UpsertAsync(UserSub, new RegisterDeviceTokenRequest("oldest", "android"));
    clock.Advance(TimeSpan.FromMinutes(1));
    await sut.UpsertAsync(UserSub, new RegisterDeviceTokenRequest("middle", "ios"));
    clock.Advance(TimeSpan.FromMinutes(1));
    await sut.UpsertAsync(UserSub, new RegisterDeviceTokenRequest("newest", "web"));

    var list = await sut.ListByUserAsync(UserSub);

    list.Should().HaveCount(3);
    list[0].DeviceToken.Should().Be("newest");
    list[1].DeviceToken.Should().Be("middle");
    list[2].DeviceToken.Should().Be("oldest");
  }

  [Fact]
  public async Task ListByUserAsync_Should_ReturnEmpty_WhenNoRegistrationsExistAsync()
  {
    var clock = new FakeClock(T0);
    var sut = new InMemoryDeviceTokenRegistry(clock);

    var list = await sut.ListByUserAsync("unknown-user");

    list.Should().BeEmpty();
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
  }
}
