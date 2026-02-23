using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Aarogya.Api.Caching;
using Aarogya.Domain.Entities;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;

namespace Aarogya.Api.Authentication;

[SuppressMessage(
  "Performance",
  "CA1515:Consider making public types internal",
  Justification = "Resolved from DI in middleware via RequestServices.")]
public interface IUserAutoProvisioningService
{
  public Task EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

internal sealed class UserAutoProvisioningService(
  IUserRepository userRepository,
  IUnitOfWork unitOfWork,
  IEntityCacheService cacheService,
  ILogger<UserAutoProvisioningService> logger)
  : IUserAutoProvisioningService
{
  private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
  private const string CacheKeyPrefix = "user_exists:";

  public async Task EnsureUserExistsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
  {
    var sub = principal.FindFirstValue("sub");
    if (string.IsNullOrWhiteSpace(sub))
    {
      return;
    }

    if (sub.StartsWith("lab:", StringComparison.Ordinal))
    {
      return;
    }

    var cacheKey = string.Concat(CacheKeyPrefix, sub);
    var cached = await cacheService.GetAsync<bool>(cacheKey, cancellationToken);
    if (cached)
    {
      return;
    }

    var existingUser = await userRepository.GetByExternalAuthIdAsync(sub, cancellationToken);
    if (existingUser is not null)
    {
      await cacheService.SetAsync(cacheKey, true, CacheTtl, cancellationToken);
      return;
    }

    var email = principal.FindFirstValue("email") ?? string.Empty;
    var givenName = principal.FindFirstValue("given_name") ?? string.Empty;
    var familyName = principal.FindFirstValue("family_name") ?? string.Empty;

    var user = new User
    {
      Id = Guid.NewGuid(),
      ExternalAuthId = sub,
      Role = UserRole.Patient,
      FirstName = !string.IsNullOrWhiteSpace(givenName) ? givenName : "User",
      LastName = !string.IsNullOrWhiteSpace(familyName) ? familyName : sub[..Math.Min(sub.Length, 8)],
      Email = !string.IsNullOrWhiteSpace(email) ? email : $"{sub}@placeholder.local",
      IsActive = true
    };

    await userRepository.AddAsync(user, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    logger.LogInformation("Auto-provisioned user {Sub} with role {Role}", sub, UserRole.Patient);
    await cacheService.SetAsync(cacheKey, true, CacheTtl, cancellationToken);
  }
}
