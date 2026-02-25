using System.Text.Json;
using Aarogya.Domain.Enums;
using Aarogya.Domain.Repositories;

namespace Aarogya.Api.Authentication;

internal sealed class RegistrationRequiredMiddleware(RequestDelegate next)
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  private static readonly HashSet<string> ExemptPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
  {
    "/api/auth",
    "/health",
    "/swagger"
  };

  private static bool IsExemptPath(PathString path)
  {
    var pathValue = path.Value;
    if (string.IsNullOrEmpty(pathValue))
    {
      return true;
    }

    if (ExemptPathPrefixes.Any(prefix => pathValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
    {
      return true;
    }

    // Registration and status endpoints are exempt
    return pathValue.Equals("/api/v1/users/register", StringComparison.OrdinalIgnoreCase)
      || pathValue.Equals("/api/v1/users/me/registration-status", StringComparison.OrdinalIgnoreCase);
  }

  public async Task InvokeAsync(HttpContext context)
  {
    if (context.User.Identity?.IsAuthenticated != true || IsExemptPath(context.Request.Path))
    {
      await next(context);
      return;
    }

    var sub = context.User.FindFirst("sub")?.Value;
    if (string.IsNullOrWhiteSpace(sub) || sub.StartsWith("lab:", StringComparison.Ordinal))
    {
      await next(context);
      return;
    }

    var userRepository = context.RequestServices.GetService<IUserRepository>();
    if (userRepository is null)
    {
      await next(context);
      return;
    }

    var user = await userRepository.GetByExternalAuthIdAsync(sub, context.RequestAborted);

    if (user is null)
    {
      await WriteJsonResponseAsync(context, StatusCodes.Status403Forbidden, "registration_required",
        "User registration is required. Please complete registration before accessing the API.");
      return;
    }

    if (user.RegistrationStatus == RegistrationStatus.PendingApproval)
    {
      await WriteJsonResponseAsync(context, StatusCodes.Status403Forbidden, "registration_pending_approval",
        "Your registration is pending admin approval.");
      return;
    }

    if (user.RegistrationStatus == RegistrationStatus.Rejected)
    {
      await WriteJsonResponseAsync(context, StatusCodes.Status403Forbidden, "registration_rejected",
        "Your registration has been rejected. Please contact support.");
      return;
    }

    await next(context);
  }

  private static async Task WriteJsonResponseAsync(HttpContext context, int statusCode, string error, string message)
  {
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(
      JsonSerializer.Serialize(new { error, message }, JsonOptions),
      context.RequestAborted);
  }
}
