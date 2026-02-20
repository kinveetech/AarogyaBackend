namespace Aarogya.Api.Caching;

internal interface IEntityCacheService
{
  public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

  public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

  public Task RemoveAsync(string key, CancellationToken cancellationToken = default);

  public Task<string> GetNamespaceVersionAsync(string cacheNamespace, CancellationToken cancellationToken = default);

  public Task BumpNamespaceVersionAsync(string cacheNamespace, CancellationToken cancellationToken = default);
}
