using System.Security.Claims;
using Aarogya.Api.Authentication;
using Aarogya.Api.Caching;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aarogya.Api.Tests;

public sealed class UserAutoProvisioningServiceTests
{
  [Fact]
  public async Task EnsureUserExistsAsync_ShouldSkip_WhenCachedAsync()
  {
    var (service, repo, uow, cache) = CreateService();
    cache.Setup(c => c.GetAsync<bool>("user_exists:sub-123", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var principal = CreatePrincipal("sub-123", "user@example.com");
    await service.EnsureUserExistsAsync(principal);

    repo.Verify(r => r.GetByExternalAuthIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task EnsureUserExistsAsync_ShouldCacheAndReturn_WhenUserExistsInDbAsync()
  {
    var (service, repo, uow, cache) = CreateService();
    cache.Setup(c => c.GetAsync<bool>("user_exists:sub-456", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);
    repo.Setup(r => r.GetByExternalAuthIdAsync("sub-456", It.IsAny<CancellationToken>()))
      .ReturnsAsync(new User { ExternalAuthId = "sub-456" });

    var principal = CreatePrincipal("sub-456", "user@example.com");
    await service.EnsureUserExistsAsync(principal);

    cache.Verify(c => c.SetAsync("user_exists:sub-456", true, TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Once);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task EnsureUserExistsAsync_ShouldCreateUser_WhenNotFoundAsync()
  {
    var (service, repo, uow, cache) = CreateService();
    cache.Setup(c => c.GetAsync<bool>("user_exists:sub-789", It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);
    repo.Setup(r => r.GetByExternalAuthIdAsync("sub-789", It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);
    repo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var principal = CreatePrincipal("sub-789", "new@example.com", "Jane", "Doe");
    await service.EnsureUserExistsAsync(principal);

    repo.Verify(r => r.AddAsync(It.Is<User>(u =>
      u.ExternalAuthId == "sub-789"
      && u.Email == "new@example.com"
      && u.FirstName == "Jane"
      && u.LastName == "Doe"
      && u.Role == UserRole.Patient
      && u.IsActive), It.IsAny<CancellationToken>()), Times.Once);
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    cache.Verify(c => c.SetAsync("user_exists:sub-789", true, TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task EnsureUserExistsAsync_ShouldHandleMissingClaims_GracefullyAsync()
  {
    var (service, repo, uow, cache) = CreateService();
    cache.Setup(c => c.GetAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);
    repo.Setup(r => r.GetByExternalAuthIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((User?)null);
    repo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim("sub", "sub-minimal")
    ], "TestAuth"));

    await service.EnsureUserExistsAsync(principal);

    repo.Verify(r => r.AddAsync(It.Is<User>(u =>
      u.ExternalAuthId == "sub-minimal"
      && u.FirstName == "User"
      && u.Email.Contains("@placeholder.local")), It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task EnsureUserExistsAsync_ShouldSkip_WhenSubMissingAsync()
  {
    var (service, repo, _, _) = CreateService();

    var principal = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));
    await service.EnsureUserExistsAsync(principal);

    repo.Verify(r => r.GetByExternalAuthIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task EnsureUserExistsAsync_ShouldSkip_WhenApiKeySubjectAsync()
  {
    var (service, repo, _, _) = CreateService();

    var principal = CreatePrincipal("lab:partner-1", "lab@example.com");
    await service.EnsureUserExistsAsync(principal);

    repo.Verify(r => r.GetByExternalAuthIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  private static (UserAutoProvisioningService Service, Mock<IUserRepository> Repo, Mock<IUnitOfWork> Uow, Mock<IEntityCacheService> Cache)
    CreateService()
  {
    var repo = new Mock<IUserRepository>();
    var uow = new Mock<IUnitOfWork>();
    var cache = new Mock<IEntityCacheService>();
    var logger = new Mock<ILogger<UserAutoProvisioningService>>();
    var service = new UserAutoProvisioningService(repo.Object, uow.Object, cache.Object, logger.Object);
    return (service, repo, uow, cache);
  }

  private static ClaimsPrincipal CreatePrincipal(
    string sub,
    string email,
    string? givenName = null,
    string? familyName = null)
  {
    var claims = new List<Claim>
    {
      new("sub", sub),
      new("email", email)
    };

    if (givenName is not null)
    {
      claims.Add(new Claim("given_name", givenName));
    }

    if (familyName is not null)
    {
      claims.Add(new Claim("family_name", familyName));
    }

    return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
  }
}
