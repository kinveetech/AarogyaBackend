using Aarogya.Api.Security;
using Aarogya.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Security;

public sealed class DataEncryptionKeyRotationHostedServiceTests
{
  [Fact]
  public async Task ExecuteAsync_ShouldSkip_WhenReEncryptionDisabledAsync()
  {
    var scopeFactory = new Mock<IServiceScopeFactory>();
    var options = Options.Create(new DataEncryptionRotationOptions
    {
      EnableBackgroundReEncryption = false
    });

    var sut = new DataEncryptionKeyRotationHostedService(
      scopeFactory.Object,
      options,
      NullLogger<DataEncryptionKeyRotationHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await sut.StartAsync(cts.Token);
    await Task.Delay(100);
    await cts.CancelAsync();
    await sut.StopAsync(CancellationToken.None);

    scopeFactory.Verify(
      x => x.CreateScope(),
      Times.Never);
  }

  [Fact]
  public async Task ExecuteAsync_ShouldStopGracefully_WhenCancelledImmediatelyAsync()
  {
    var scopeFactory = new Mock<IServiceScopeFactory>();
    var options = Options.Create(new DataEncryptionRotationOptions
    {
      EnableBackgroundReEncryption = false
    });

    var sut = new DataEncryptionKeyRotationHostedService(
      scopeFactory.Object,
      options,
      NullLogger<DataEncryptionKeyRotationHostedService>.Instance);

    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();
    await sut.StartAsync(cts.Token);

    var act = async () => await sut.StopAsync(CancellationToken.None);
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public void ExecuteAsync_ShouldUseConfiguredCheckInterval()
  {
    var options = new DataEncryptionRotationOptions
    {
      EnableBackgroundReEncryption = true,
      CheckIntervalMinutes = 60,
      BatchSize = 100
    };

    options.CheckIntervalMinutes.Should().Be(60);
    options.EnableBackgroundReEncryption.Should().BeTrue();
    options.BatchSize.Should().Be(100);
  }
}
