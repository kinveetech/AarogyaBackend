using System.Security.Claims;
using Aarogya.Api.Authorization;

namespace Aarogya.Api.Endpoints;

internal static class V1EndpointMappings
{
  public static IServiceCollection AddV1EndpointGroupServices(this IServiceCollection services)
  {
    services.AddSingleton<IUsersEndpointService, InMemoryUsersEndpointService>();
    services.AddSingleton<IReportsEndpointService, InMemoryReportsEndpointService>();
    services.AddSingleton<IAccessGrantsEndpointService, InMemoryAccessGrantsEndpointService>();
    services.AddSingleton<IEmergencyContactsEndpointService, InMemoryEmergencyContactsEndpointService>();
    return services;
  }

  public static IEndpointRouteBuilder MapV1EndpointGroups(this IEndpointRouteBuilder app)
  {
    var v1 = app.MapGroup("/api/v1")
      .WithOpenApi();

    MapUsers(v1);
    MapReports(v1);
    MapAccessGrants(v1);
    MapEmergencyContacts(v1);

    return app;
  }

  private static void MapUsers(RouteGroupBuilder v1)
  {
    var users = v1.MapGroup("/users")
      .WithTags("users")
      .RequireAuthorization(AarogyaPolicies.AnyRegisteredRole);

    users.MapGet("/me", (HttpContext context, IUsersEndpointService service)
      => TypedResults.Ok(service.GetCurrentUser(context.User)));
  }

  private static void MapReports(RouteGroupBuilder v1)
  {
    var reports = v1.MapGroup("/reports")
      .WithTags("reports")
      .RequireAuthorization(AarogyaPolicies.AnyRegisteredRole);

    reports.MapGet("/", async (HttpContext context, IReportsEndpointService service, CancellationToken ct) =>
    {
      var userSub = GetRequiredSub(context.User);
      if (userSub is null)
      {
        return Results.Unauthorized();
      }

      var result = await service.GetForUserAsync(userSub, ct);
      return Results.Ok(result);
    });

    reports.MapPost("/", async (CreateReportRequest request, HttpContext context, IReportsEndpointService service, CancellationToken ct) =>
    {
      if (string.IsNullOrWhiteSpace(request.Title))
      {
        return Results.BadRequest(new ApiError("Title is required."));
      }

      var userSub = GetRequiredSub(context.User);
      if (userSub is null)
      {
        return Results.Unauthorized();
      }

      var result = await service.AddForUserAsync(userSub, request, ct);
      return Results.Created($"/api/v1/reports/{result.ReportId}", result);
    });
  }

  private static void MapAccessGrants(RouteGroupBuilder v1)
  {
    var grants = v1.MapGroup("/access-grants")
      .WithTags("access-grants")
      .RequireAuthorization(AarogyaPolicies.Patient);

    grants.MapGet("/", async (HttpContext context, IAccessGrantsEndpointService service, CancellationToken ct) =>
    {
      var patientSub = GetRequiredSub(context.User);
      if (patientSub is null)
      {
        return Results.Unauthorized();
      }

      var result = await service.GetForPatientAsync(patientSub, ct);
      return Results.Ok(result);
    });

    grants.MapPost("/", async (CreateAccessGrantRequest request, HttpContext context, IAccessGrantsEndpointService service, CancellationToken ct) =>
    {
      if (string.IsNullOrWhiteSpace(request.DoctorSub)
        || request.ReportIds is null
        || request.ReportIds.Count == 0)
      {
        return Results.BadRequest(new ApiError("DoctorSub and at least one report ID are required."));
      }

      var patientSub = GetRequiredSub(context.User);
      if (patientSub is null)
      {
        return Results.Unauthorized();
      }

      var created = await service.CreateAsync(patientSub, request, ct);
      return Results.Created($"/api/v1/access-grants/{created.GrantId}", created);
    });

    grants.MapDelete("/{grantId:guid}", async (Guid grantId, HttpContext context, IAccessGrantsEndpointService service, CancellationToken ct) =>
    {
      var patientSub = GetRequiredSub(context.User);
      if (patientSub is null)
      {
        return Results.Unauthorized();
      }

      var revoked = await service.RevokeAsync(patientSub, grantId, ct);
      return revoked ? Results.NoContent() : Results.NotFound();
    });
  }

  private static void MapEmergencyContacts(RouteGroupBuilder v1)
  {
    var contacts = v1.MapGroup("/emergency-contacts")
      .WithTags("emergency-contacts")
      .RequireAuthorization(AarogyaPolicies.AnyRegisteredRole);

    contacts.MapGet("/", async (HttpContext context, IEmergencyContactsEndpointService service, CancellationToken ct) =>
    {
      var userSub = GetRequiredSub(context.User);
      if (userSub is null)
      {
        return Results.Unauthorized();
      }

      var result = await service.GetForUserAsync(userSub, ct);
      return Results.Ok(result);
    });

    contacts.MapPost("/", async (CreateEmergencyContactRequest request, HttpContext context, IEmergencyContactsEndpointService service, CancellationToken ct) =>
    {
      if (string.IsNullOrWhiteSpace(request.Name)
        || string.IsNullOrWhiteSpace(request.PhoneNumber)
        || string.IsNullOrWhiteSpace(request.Relationship))
      {
        return Results.BadRequest(new ApiError("Name, phone number and relationship are required."));
      }

      var userSub = GetRequiredSub(context.User);
      if (userSub is null)
      {
        return Results.Unauthorized();
      }

      var created = await service.AddForUserAsync(userSub, request, ct);
      return Results.Created($"/api/v1/emergency-contacts/{created.ContactId}", created);
    });

    contacts.MapDelete("/{contactId:guid}", async (Guid contactId, HttpContext context, IEmergencyContactsEndpointService service, CancellationToken ct) =>
    {
      var userSub = GetRequiredSub(context.User);
      if (userSub is null)
      {
        return Results.Unauthorized();
      }

      var deleted = await service.DeleteForUserAsync(userSub, contactId, ct);
      return deleted ? Results.NoContent() : Results.NotFound();
    });
  }

  private static string? GetRequiredSub(ClaimsPrincipal principal)
  {
    var sub = principal.FindFirstValue("sub");
    return string.IsNullOrWhiteSpace(sub) ? null : sub;
  }
}

internal sealed record UserProfileResponse(string Sub, string Email, IReadOnlyList<string> Roles);
internal sealed record CreateReportRequest(string Title);
internal sealed record ReportSummaryResponse(Guid ReportId, string Title, string Status, DateTimeOffset CreatedAt);
internal sealed record CreateAccessGrantRequest(string DoctorSub, IReadOnlyList<Guid> ReportIds, DateTimeOffset ExpiresAt);
internal sealed record AccessGrantResponse(Guid GrantId, string DoctorSub, IReadOnlyList<Guid> ReportIds, DateTimeOffset ExpiresAt, bool Revoked);
internal sealed record CreateEmergencyContactRequest(string Name, string PhoneNumber, string Relationship);
internal sealed record EmergencyContactResponse(Guid ContactId, string Name, string PhoneNumber, string Relationship);
internal sealed record ApiError(string Error);
