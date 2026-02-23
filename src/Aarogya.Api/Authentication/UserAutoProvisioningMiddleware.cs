namespace Aarogya.Api.Authentication;

internal sealed class UserAutoProvisioningMiddleware(RequestDelegate next)
{
  public async Task InvokeAsync(HttpContext context)
  {
    if (context.User.Identity?.IsAuthenticated == true)
    {
      var provisioningService = context.RequestServices.GetService<IUserAutoProvisioningService>();
      if (provisioningService is not null)
      {
        await provisioningService.EnsureUserExistsAsync(context.User, context.RequestAborted);
      }
    }

    await next(context);
  }
}
