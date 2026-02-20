namespace Aarogya.Api.Endpoints;

internal static class ApiVersioningExtensions
{
  public static IApplicationBuilder UseAarogyaApiVersioning(this IApplicationBuilder app)
  {
    app.Use(async (context, next) =>
    {
      context.Response.Headers.Append("api-supported-versions", "v1");
      context.Response.Headers.Append("api-selected-version", "v1");
      await next();
    });

    return app;
  }
}
