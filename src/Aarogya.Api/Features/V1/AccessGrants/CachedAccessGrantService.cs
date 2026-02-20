using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.AccessGrants;

internal sealed class CachedAccessGrantService(
  AccessGrantService innerService,
  IEntityCacheService cacheService,
  IOptions<EntityCacheOptions> cacheOptions)
  : IAccessGrantService
{
  private readonly TimeSpan _ttl = TimeSpan.FromSeconds(cacheOptions.Value.AccessGrantTtlSeconds);

  public async Task<IReadOnlyList<AccessGrantResponse>> GetForPatientAsync(string patientSub, CancellationToken cancellationToken = default)
  {
    var version = await cacheService.GetNamespaceVersionAsync(EntityCacheNamespaces.AccessGrantListings, cancellationToken);
    var cacheKey = EntityCacheKeys.AccessGrantListForPatient(patientSub, version);

    var cached = await cacheService.GetAsync<AccessGrantResponse[]>(cacheKey, cancellationToken);
    if (cached is not null)
    {
      return cached;
    }

    var response = await innerService.GetForPatientAsync(patientSub, cancellationToken);
    await cacheService.SetAsync(cacheKey, response.ToArray(), _ttl, cancellationToken);
    return response;
  }

  public async Task<IReadOnlyList<AccessGrantResponse>> GetForDoctorAsync(string doctorSub, CancellationToken cancellationToken = default)
  {
    var version = await cacheService.GetNamespaceVersionAsync(EntityCacheNamespaces.AccessGrantListings, cancellationToken);
    var cacheKey = EntityCacheKeys.AccessGrantListForDoctor(doctorSub, version);

    var cached = await cacheService.GetAsync<AccessGrantResponse[]>(cacheKey, cancellationToken);
    if (cached is not null)
    {
      return cached;
    }

    var response = await innerService.GetForDoctorAsync(doctorSub, cancellationToken);
    await cacheService.SetAsync(cacheKey, response.ToArray(), _ttl, cancellationToken);
    return response;
  }

  public async Task<AccessGrantResponse> CreateAsync(
    string patientSub,
    CreateAccessGrantRequest request,
    CancellationToken cancellationToken = default)
  {
    var response = await innerService.CreateAsync(patientSub, request, cancellationToken);
    await InvalidateGrantAndReportListCachesAsync(cancellationToken);
    return response;
  }

  public async Task<bool> RevokeAsync(string patientSub, Guid grantId, CancellationToken cancellationToken = default)
  {
    var revoked = await innerService.RevokeAsync(patientSub, grantId, cancellationToken);
    if (revoked)
    {
      await InvalidateGrantAndReportListCachesAsync(cancellationToken);
    }

    return revoked;
  }

  private async Task InvalidateGrantAndReportListCachesAsync(CancellationToken cancellationToken)
  {
    await cacheService.BumpNamespaceVersionAsync(EntityCacheNamespaces.AccessGrantListings, cancellationToken);
    await cacheService.BumpNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, cancellationToken);
  }
}
