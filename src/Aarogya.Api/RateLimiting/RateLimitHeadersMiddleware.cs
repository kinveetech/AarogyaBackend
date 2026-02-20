using Aarogya.Api.Authentication;

namespace Aarogya.Api.RateLimiting;

internal sealed class RateLimitHeadersMiddleware(
  RequestDelegate next,
  IRateLimitHeaderCounter counter,
  IUtcClock clock)
{
  public async Task InvokeAsync(HttpContext context)
  {
    await next(context);

    if (context.Items.TryGetValue(RateLimitDescriptor.HttpContextItemKey, out var itemValue)
      && itemValue is RateLimitDescriptor descriptor)
    {
      var snapshot = counter.TrackAccepted(descriptor, clock.UtcNow);
      ApplyHeaders(context.Response.Headers, snapshot);
    }
  }

  public static void ApplyHeaders(IHeaderDictionary headers, RateLimitHeaderSnapshot snapshot)
  {
    headers["X-RateLimit-Limit"] = snapshot.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
    headers["X-RateLimit-Remaining"] = snapshot.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
    headers["X-RateLimit-Reset"] = snapshot.ResetAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
  }
}
