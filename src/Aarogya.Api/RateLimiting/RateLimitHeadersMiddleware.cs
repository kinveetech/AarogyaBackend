using Aarogya.Api.Authentication;

namespace Aarogya.Api.RateLimiting;

internal sealed class RateLimitHeadersMiddleware(
  RequestDelegate next,
  IRateLimitHeaderCounter counter,
  IUtcClock clock)
{
  public async Task InvokeAsync(HttpContext context)
  {
    if (context.Items.TryGetValue(RateLimitDescriptor.HttpContextItemKey, out var itemValue)
      && itemValue is RateLimitDescriptor descriptor)
    {
      var snapshot = counter.TrackAccepted(descriptor, clock.UtcNow);
      context.Response.OnStarting(state =>
      {
        var (httpContext, currentSnapshot) = ((HttpContext, RateLimitHeaderSnapshot))state;
        ApplyHeaders(httpContext.Response.Headers, currentSnapshot);
        return Task.CompletedTask;
      }, (context, snapshot));
    }

    await next(context);
  }

  public static void ApplyHeaders(IHeaderDictionary headers, RateLimitHeaderSnapshot snapshot)
  {
    headers["X-RateLimit-Limit"] = snapshot.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
    headers["X-RateLimit-Remaining"] = snapshot.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
    headers["X-RateLimit-Reset"] = snapshot.ResetAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
  }
}
