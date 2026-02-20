using System.Security.Claims;
using System.Threading.RateLimiting;
using Aarogya.Api.Configuration;
using Microsoft.AspNetCore.RateLimiting;

namespace Aarogya.Api.RateLimiting;

internal static class RateLimitingServiceCollectionExtensions
{
  public static IServiceCollection AddAarogyaRateLimiting(this IServiceCollection services, RateLimitingOptions options)
  {
    services.AddSingleton<IRateLimitHeaderCounter, InMemoryRateLimitHeaderCounter>();

    services.AddRateLimiter(rateLimiterOptions =>
    {
      rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      rateLimiterOptions.OnRejected = async (context, token) =>
      {
        if (context.HttpContext.Items.TryGetValue(RateLimitDescriptor.HttpContextItemKey, out var itemValue)
          && itemValue is RateLimitDescriptor descriptor)
        {
          var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
            ? DateTimeOffset.UtcNow.Add(delay)
            : DateTimeOffset.UtcNow.Add(descriptor.Window);

          RateLimitHeadersMiddleware.ApplyHeaders(
            context.HttpContext.Response.Headers,
            new RateLimitHeaderSnapshot(descriptor.PermitLimit, 0, retryAfter));
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
          new { error = "Too many requests. Please retry later." },
          cancellationToken: token);
      };

      AddPolicy(rateLimiterOptions, RateLimitPolicyNames.Auth, options.Auth);
      AddPolicy(rateLimiterOptions, RateLimitPolicyNames.ApiV1, options.ApiV1);
    });

    return services;
  }

  private static void AddPolicy(RateLimiterOptions options, string policyName, RateLimitPolicyOptions policy)
  {
    var window = TimeSpan.FromSeconds(policy.WindowSeconds);
    var isSliding = string.Equals(policy.Strategy, "sliding", StringComparison.OrdinalIgnoreCase);

    options.AddPolicy(policyName, httpContext =>
    {
      var partitionKey = GetPartitionKey(httpContext, policy.PreferPerUserLimits);
      var descriptor = new RateLimitDescriptor(
        policyName,
        partitionKey,
        policy.PermitLimit,
        window,
        isSliding,
        policy.SegmentsPerWindow);

      httpContext.Items[RateLimitDescriptor.HttpContextItemKey] = descriptor;

      if (isSliding)
      {
        return RateLimitPartition.GetSlidingWindowLimiter(
          partitionKey,
          _ => new SlidingWindowRateLimiterOptions
          {
            PermitLimit = policy.PermitLimit,
            Window = window,
            SegmentsPerWindow = policy.SegmentsPerWindow,
            QueueLimit = policy.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
          });
      }

      return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = policy.PermitLimit,
          Window = window,
          QueueLimit = policy.QueueLimit,
          QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
          AutoReplenishment = true
        });
    });
  }

  private static string GetPartitionKey(HttpContext context, bool preferPerUserLimits)
  {
    if (preferPerUserLimits)
    {
      var subject = context.User.FindFirstValue("sub");
      if (!string.IsNullOrWhiteSpace(subject))
      {
        return $"user:{subject}";
      }
    }

    var ip = context.Connection.RemoteIpAddress?.ToString();
    return string.IsNullOrWhiteSpace(ip) ? "ip:unknown" : $"ip:{ip}";
  }
}
