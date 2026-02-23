using Aarogya.Api.Authentication;
using Aarogya.Api.Configuration;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Repositories;
using Aarogya.Domain.Specifications;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class DatabaseApiKeyServiceTests
{
  [Fact]
  public async Task IssueKeyAsync_ShouldPersistAndReturnKey_ForValidRequestAsync()
  {
    var (service, repo, uow, _) = CreateService();
    repo.Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var result = await service.IssueKeyAsync(new ApiKeyIssueRequest("lab-1", "Acme Labs"));

    result.Success.Should().BeTrue();
    result.ApiKey.Should().NotBeNullOrWhiteSpace();
    result.KeyId.Should().NotBeNullOrWhiteSpace();
    result.PartnerId.Should().Be("lab-1");
    result.PartnerName.Should().Be("Acme Labs");
    result.ExpiresAt.Should().NotBeNull();
    repo.Verify(r => r.AddAsync(It.Is<ApiKey>(k =>
      k.PartnerId == "lab-1" && k.PartnerName == "Acme Labs" && k.KeyHash.Length == 64),
      It.IsAny<CancellationToken>()), Times.Once);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task IssueKeyAsync_ShouldFail_WhenPartnerIdEmptyAsync()
  {
    var (service, _, _, _) = CreateService();

    var result = await service.IssueKeyAsync(new ApiKeyIssueRequest("", "Acme Labs"));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Partner ID");
  }

  [Fact]
  public async Task IssueKeyAsync_ShouldFail_WhenPartnerNameEmptyAsync()
  {
    var (service, _, _, _) = CreateService();

    var result = await service.IssueKeyAsync(new ApiKeyIssueRequest("lab-1", ""));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Partner name");
  }

  [Fact]
  public async Task ValidateKeyAsync_ShouldSucceed_ForValidKeyAsync()
  {
    var (service, repo, uow, _) = CreateService();
    repo.Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    ApiKey? capturedEntity = null;
    repo.Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
      .Callback<ApiKey, CancellationToken>((entity, _) => capturedEntity = entity)
      .Returns(Task.CompletedTask);

    var reissued = await service.IssueKeyAsync(new ApiKeyIssueRequest("lab-1", "Acme Labs"));

    repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<ApiKeyByHashSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(capturedEntity);

    var validated = await service.ValidateKeyAsync(reissued.ApiKey!);

    validated.Success.Should().BeTrue();
    validated.PartnerId.Should().Be("lab-1");
    validated.PartnerName.Should().Be("Acme Labs");
  }

  [Fact]
  public async Task ValidateKeyAsync_ShouldFail_WhenKeyNotFoundAsync()
  {
    var (service, repo, _, _) = CreateService();
    repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<ApiKeyByHashSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((ApiKey?)null);

    var result = await service.ValidateKeyAsync("nonexistent-key");

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Invalid");
  }

  [Fact]
  public async Task ValidateKeyAsync_ShouldFail_WhenKeyExpiredAsync()
  {
    var now = new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero);
    var (service, repo, _, _) = CreateService(now);

    var expiredKey = new ApiKey
    {
      Id = Guid.NewGuid(),
      KeyHash = "somehash",
      KeyPrefix = "aarogya_lab_",
      PartnerId = "lab-1",
      PartnerName = "Acme Labs",
      ExpiresAt = now.AddDays(-1),
      IsRevoked = false
    };

    repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<ApiKeyByHashSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(expiredKey);

    var result = await service.ValidateKeyAsync("some-api-key");

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("revoked or expired");
  }

  [Fact]
  public async Task ValidateKeyAsync_ShouldFail_WhenKeyRevokedAsync()
  {
    var now = new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero);
    var (service, repo, _, _) = CreateService(now);

    var revokedKey = new ApiKey
    {
      Id = Guid.NewGuid(),
      KeyHash = "somehash",
      KeyPrefix = "aarogya_lab_",
      PartnerId = "lab-1",
      PartnerName = "Acme Labs",
      ExpiresAt = now.AddDays(30),
      IsRevoked = true,
      RevokedAt = now.AddDays(-1)
    };

    repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<ApiKeyByHashSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(revokedKey);

    var result = await service.ValidateKeyAsync("some-api-key");

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("revoked or expired");
  }

  [Fact]
  public async Task ValidateKeyAsync_ShouldRateLimit_PerKeyAsync()
  {
    var now = new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero);
    var apiKeyOptions = new ApiKeyOptions
    {
      KeyPrefix = "aarogya_lab_test_",
      MaxRequestsPerWindow = 2,
      RateLimitWindowSeconds = 60,
      DefaultKeyLifetimeDays = 365,
      RotationOverlapMinutes = 60
    };
    var (service, repo, _, _) = CreateService(now, apiKeyOptions);

    var validKey = new ApiKey
    {
      Id = Guid.NewGuid(),
      KeyHash = "somehash",
      KeyPrefix = "aarogya_lab_",
      PartnerId = "lab-2",
      PartnerName = "Rate Limit Lab",
      ExpiresAt = now.AddDays(30),
      IsRevoked = false
    };

    repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<ApiKeyByHashSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(validKey);

    var first = await service.ValidateKeyAsync("some-api-key");
    var second = await service.ValidateKeyAsync("some-api-key");
    var third = await service.ValidateKeyAsync("some-api-key");

    first.Success.Should().BeTrue();
    second.Success.Should().BeTrue();
    third.Success.Should().BeFalse();
    third.IsRateLimited.Should().BeTrue();
  }

  [Fact]
  public async Task RotateKeyAsync_ShouldCreateNewKeyAndShortenOldExpiryAsync()
  {
    var now = new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero);
    var (service, repo, uow, _) = CreateService(now);

    var existingKey = new ApiKey
    {
      Id = Guid.NewGuid(),
      KeyHash = "oldhash",
      KeyPrefix = "aarogya_lab_",
      PartnerId = "lab-3",
      PartnerName = "Rotate Lab",
      ExpiresAt = now.AddDays(365),
      IsRevoked = false
    };

    repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<ActiveApiKeyByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(existingKey);
    repo.Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var result = await service.RotateKeyAsync(new ApiKeyRotateRequest(existingKey.Id.ToString("N")));

    result.Success.Should().BeTrue();
    result.ApiKey.Should().NotBeNullOrWhiteSpace();
    result.PreviousKeyValidUntil.Should().NotBeNull();
    result.PartnerId.Should().Be("lab-3");
    result.PartnerName.Should().Be("Rotate Lab");
    repo.Verify(r => r.Update(existingKey), Times.Once);
    repo.Verify(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Once);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task RotateKeyAsync_ShouldFail_WhenKeyNotFoundAsync()
  {
    var now = new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero);
    var (service, repo, _, _) = CreateService(now);

    repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<ActiveApiKeyByIdSpecification>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((ApiKey?)null);

    var result = await service.RotateKeyAsync(new ApiKeyRotateRequest(Guid.NewGuid().ToString("N")));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("not found");
  }

  [Fact]
  public async Task RotateKeyAsync_ShouldFail_WhenKeyIdInvalidFormatAsync()
  {
    var (service, _, _, _) = CreateService();

    var result = await service.RotateKeyAsync(new ApiKeyRotateRequest("not-a-guid"));

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Invalid key ID");
  }

  [Fact]
  public async Task ValidateKeyAsync_ShouldFail_WhenKeyEmptyAsync()
  {
    var (service, _, _, _) = CreateService();

    var result = await service.ValidateKeyAsync("");

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("required");
  }

  private static (DatabaseApiKeyService Service, Mock<IApiKeyRepository> Repo, Mock<IUnitOfWork> Uow, FakeClock Clock) CreateService(
    DateTimeOffset? now = null,
    ApiKeyOptions? apiKeyOptions = null)
  {
    var clock = new FakeClock(now ?? new DateTimeOffset(2026, 02, 20, 0, 0, 0, TimeSpan.Zero));
    var repo = new Mock<IApiKeyRepository>();
    var uow = new Mock<IUnitOfWork>();
    var opts = apiKeyOptions ?? new ApiKeyOptions
    {
      KeyPrefix = "aarogya_lab_test_",
      MaxRequestsPerWindow = 120,
      RateLimitWindowSeconds = 60,
      DefaultKeyLifetimeDays = 365,
      RotationOverlapMinutes = 1440
    };
    var rateLimiter = new ApiKeyRateLimiter(Options.Create(opts), clock);
    var service = new DatabaseApiKeyService(repo.Object, uow.Object, Options.Create(opts), clock, rateLimiter);
    return (service, repo, uow, clock);
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IUtcClock
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
  }
}
