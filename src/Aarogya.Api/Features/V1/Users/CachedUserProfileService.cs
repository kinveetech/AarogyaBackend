using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Users;

internal sealed class CachedUserProfileService(
  UserProfileService innerService,
  IEntityCacheService cacheService,
  IOptions<EntityCacheOptions> cacheOptions)
  : IUserProfileService
{
  private readonly TimeSpan _ttl = TimeSpan.FromSeconds(cacheOptions.Value.UserProfileTtlSeconds);

  public async Task<UserProfileResponse> GetCurrentUserAsync(string userSub, CancellationToken cancellationToken = default)
  {
    var cacheKey = EntityCacheKeys.UserProfile(userSub);
    var cached = await cacheService.GetAsync<UserProfileResponse>(cacheKey, cancellationToken);
    if (cached is not null)
    {
      return cached;
    }

    var response = await innerService.GetCurrentUserAsync(userSub, cancellationToken);
    await cacheService.SetAsync(cacheKey, response, _ttl, cancellationToken);
    return response;
  }

  public async Task<UserProfileResponse> UpdateCurrentUserAsync(
    string userSub,
    UpdateUserProfileRequest request,
    CancellationToken cancellationToken = default)
  {
    var response = await innerService.UpdateCurrentUserAsync(userSub, request, cancellationToken);
    await cacheService.RemoveAsync(EntityCacheKeys.UserProfile(userSub), cancellationToken);
    return response;
  }

  public async Task<AadhaarVerificationResponse> VerifyCurrentUserAadhaarAsync(
    string userSub,
    VerifyAadhaarRequest request,
    CancellationToken cancellationToken = default)
  {
    var response = await innerService.VerifyCurrentUserAadhaarAsync(userSub, request, cancellationToken);
    await cacheService.RemoveAsync(EntityCacheKeys.UserProfile(userSub), cancellationToken);
    return response;
  }
}
