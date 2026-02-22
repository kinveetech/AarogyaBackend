using System.Text;
using System.Text.Json;
using Aarogya.Api.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests.Caching;

public sealed class DistributedEntityCacheServiceTests
{
  private const string TestKey = "cache:test:key1";

  [Fact]
  public async Task GetAsync_Should_ReturnDeserializedValue_WhenCacheHitAsync()
  {
    var cache = new Mock<IDistributedCache>();
    var payload = JsonSerializer.Serialize(new TestDto("hello", 42));
    cache.Setup(x => x.GetAsync(TestKey, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Encoding.UTF8.GetBytes(payload));
    var sut = CreateService(cache.Object);

    var result = await sut.GetAsync<TestDto>(TestKey);

    result.Should().NotBeNull();
    result!.Name.Should().Be("hello");
    result.Value.Should().Be(42);
  }

  [Fact]
  public async Task GetAsync_Should_ReturnDefault_WhenCacheMissAsync()
  {
    var cache = new Mock<IDistributedCache>();
    cache.Setup(x => x.GetAsync(TestKey, It.IsAny<CancellationToken>()))
      .ReturnsAsync((byte[]?)null);
    var sut = CreateService(cache.Object);

    var result = await sut.GetAsync<TestDto>(TestKey);

    result.Should().BeNull();
  }

  [Fact]
  public async Task GetAsync_Should_ReturnDefault_AndRemoveKey_WhenJsonCorruptedAsync()
  {
    var cache = new Mock<IDistributedCache>();
    cache.Setup(x => x.GetAsync(TestKey, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Encoding.UTF8.GetBytes("{{not valid json"));
    var sut = CreateService(cache.Object);

    var result = await sut.GetAsync<TestDto>(TestKey);

    result.Should().BeNull();
    cache.Verify(x => x.RemoveAsync(TestKey, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task GetAsync_Should_PropagateOperationCanceledExceptionAsync()
  {
    var cache = new Mock<IDistributedCache>();
    cache.Setup(x => x.GetAsync(TestKey, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());
    var sut = CreateService(cache.Object);

    var act = () => sut.GetAsync<TestDto>(TestKey);

    await act.Should().ThrowAsync<OperationCanceledException>();
  }

  [Fact]
  public async Task GetAsync_Should_ReturnDefault_WhenCacheReadFailsAsync()
  {
    var cache = new Mock<IDistributedCache>();
    cache.Setup(x => x.GetAsync(TestKey, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Redis down"));
    var sut = CreateService(cache.Object);

    var result = await sut.GetAsync<TestDto>(TestKey);

    result.Should().BeNull();
  }

  [Fact]
  public async Task GetNamespaceVersionAsync_Should_ReturnExistingVersionAsync()
  {
    var cache = new Mock<IDistributedCache>();
    var versionKey = "cache:version:test-ns";
    cache.Setup(x => x.GetAsync(versionKey, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Encoding.UTF8.GetBytes("42"));
    var sut = CreateService(cache.Object);

    var version = await sut.GetNamespaceVersionAsync("test-ns");

    version.Should().Be("42");
  }

  [Fact]
  public async Task GetNamespaceVersionAsync_Should_InitializeToOne_WhenMissingAsync()
  {
    var cache = new Mock<IDistributedCache>();
    cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((byte[]?)null);
    var sut = CreateService(cache.Object);

    var version = await sut.GetNamespaceVersionAsync("test-ns");

    version.Should().Be("1");
    cache.Verify(
      x => x.SetAsync(
        "cache:version:test-ns",
        It.IsAny<byte[]>(),
        It.IsAny<DistributedCacheEntryOptions>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task BumpNamespaceVersionAsync_Should_WriteNewVersionAsync()
  {
    var cache = new Mock<IDistributedCache>();
    var sut = CreateService(cache.Object);

    await sut.BumpNamespaceVersionAsync("test-ns");

    cache.Verify(
      x => x.SetAsync(
        "cache:version:test-ns",
        It.IsAny<byte[]>(),
        It.IsAny<DistributedCacheEntryOptions>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetNamespaceVersionAsync_Should_ReturnFallback_WhenCacheFailsAsync()
  {
    var cache = new Mock<IDistributedCache>();
    cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("Redis down"));
    var sut = CreateService(cache.Object);

    var version = await sut.GetNamespaceVersionAsync("test-ns");

    version.Should().Be("fallback");
  }

  private static DistributedEntityCacheService CreateService(IDistributedCache cache)
  {
    return new DistributedEntityCacheService(cache, NullLogger<DistributedEntityCacheService>.Instance);
  }

  private sealed record TestDto(string Name, int Value);
}
