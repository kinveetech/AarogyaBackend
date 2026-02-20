using System.Globalization;
using Aarogya.Api.Caching;
using Aarogya.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Aarogya.Api.Features.V1.Reports;

internal sealed class CachedReportService(
  ReportService innerService,
  IEntityCacheService cacheService,
  IOptions<EntityCacheOptions> cacheOptions)
  : IReportService
{
  private readonly TimeSpan _ttl = TimeSpan.FromSeconds(cacheOptions.Value.ReportListingTtlSeconds);

  public async Task<ReportListResponse> GetForUserAsync(
    string userSub,
    ReportListQueryRequest request,
    CancellationToken cancellationToken = default)
  {
    var version = await cacheService.GetNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, cancellationToken);
    var cacheKey = EntityCacheKeys.ReportListing(userSub, BuildFingerprint(request), version);

    var cached = await cacheService.GetAsync<ReportListResponse>(cacheKey, cancellationToken);
    if (cached is not null)
    {
      return cached;
    }

    var response = await innerService.GetForUserAsync(userSub, request, cancellationToken);
    await cacheService.SetAsync(cacheKey, response, _ttl, cancellationToken);
    return response;
  }

  public Task<ReportDetailResponse> GetDetailForUserAsync(
    string userSub,
    Guid reportId,
    CancellationToken cancellationToken = default)
    => innerService.GetDetailForUserAsync(userSub, reportId, cancellationToken);

  public async Task<ReportSummaryResponse> AddForUserAsync(
    string userSub,
    CreateReportRequest request,
    CancellationToken cancellationToken = default)
  {
    var response = await innerService.AddForUserAsync(userSub, request, cancellationToken);
    await cacheService.BumpNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, cancellationToken);
    return response;
  }

  public Task<ReportSignedUploadUrlResponse> GetSignedUploadUrlAsync(
    string userSub,
    CreateReportUploadUrlRequest request,
    CancellationToken cancellationToken = default)
    => innerService.GetSignedUploadUrlAsync(userSub, request, cancellationToken);

  public Task<ReportSignedDownloadUrlResponse> GetSignedDownloadUrlAsync(
    string userSub,
    CreateReportDownloadUrlRequest request,
    CancellationToken cancellationToken = default)
    => innerService.GetSignedDownloadUrlAsync(userSub, request, cancellationToken);

  public async Task<bool> SoftDeleteForUserAsync(
    string userSub,
    Guid reportId,
    CancellationToken cancellationToken = default)
  {
    var deleted = await innerService.SoftDeleteForUserAsync(userSub, reportId, cancellationToken);
    if (deleted)
    {
      await cacheService.BumpNamespaceVersionAsync(EntityCacheNamespaces.ReportListings, cancellationToken);
    }

    return deleted;
  }

  private static string BuildFingerprint(ReportListQueryRequest request)
  {
    return string.Create(
      CultureInfo.InvariantCulture,
      $"{request.ReportType}|{request.Status}|{request.FromDate:O}|{request.ToDate:O}|{request.Page}|{request.PageSize}");
  }
}
