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
      => TypedResults.Ok(service.GetCurrentUser(context.User)))
      .WithName("GetCurrentUserProfileV1")
      .WithSummary("Get current user profile")
      .WithDescription("Returns the authenticated user's profile and resolved roles.")
      .Produces<UserProfileResponse>(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status401Unauthorized);
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
    })
      .WithName("ListReportsV1")
      .WithSummary("List reports")
      .WithDescription("Returns report summaries available to the authenticated user.")
      .Produces<IReadOnlyList<ReportSummaryResponse>>(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status401Unauthorized);

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
    })
      .WithName("CreateReportV1")
      .WithSummary("Create report")
      .WithDescription("Creates a report placeholder metadata entry for the authenticated user.")
      .Accepts<CreateReportRequest>("application/json")
      .Produces<ReportSummaryResponse>(StatusCodes.Status201Created)
      .Produces<ApiError>(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status401Unauthorized);
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
    })
      .WithName("ListAccessGrantsV1")
      .WithSummary("List access grants")
      .WithDescription("Returns access grants created by the authenticated patient.")
      .Produces<IReadOnlyList<AccessGrantResponse>>(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status403Forbidden);

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
    })
      .WithName("CreateAccessGrantV1")
      .WithSummary("Create access grant")
      .WithDescription("Creates a doctor access grant for selected reports.")
      .Accepts<CreateAccessGrantRequest>("application/json")
      .Produces<AccessGrantResponse>(StatusCodes.Status201Created)
      .Produces<ApiError>(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status403Forbidden);

    grants.MapDelete("/{grantId:guid}", async (Guid grantId, HttpContext context, IAccessGrantsEndpointService service, CancellationToken ct) =>
    {
      var patientSub = GetRequiredSub(context.User);
      if (patientSub is null)
      {
        return Results.Unauthorized();
      }

      var revoked = await service.RevokeAsync(patientSub, grantId, ct);
      return revoked ? Results.NoContent() : Results.NotFound();
    })
      .WithName("RevokeAccessGrantV1")
      .WithSummary("Revoke access grant")
      .WithDescription("Revokes a previously issued access grant.")
      .Produces(StatusCodes.Status204NoContent)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status403Forbidden)
      .Produces(StatusCodes.Status404NotFound);
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
    })
      .WithName("ListEmergencyContactsV1")
      .WithSummary("List emergency contacts")
      .WithDescription("Returns emergency contacts for the authenticated user.")
      .Produces<IReadOnlyList<EmergencyContactResponse>>(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status401Unauthorized);

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
    })
      .WithName("CreateEmergencyContactV1")
      .WithSummary("Create emergency contact")
      .WithDescription("Adds an emergency contact for the authenticated user.")
      .Accepts<CreateEmergencyContactRequest>("application/json")
      .Produces<EmergencyContactResponse>(StatusCodes.Status201Created)
      .Produces<ApiError>(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status401Unauthorized);

    contacts.MapDelete("/{contactId:guid}", async (Guid contactId, HttpContext context, IEmergencyContactsEndpointService service, CancellationToken ct) =>
    {
      var userSub = GetRequiredSub(context.User);
      if (userSub is null)
      {
        return Results.Unauthorized();
      }

      var deleted = await service.DeleteForUserAsync(userSub, contactId, ct);
      return deleted ? Results.NoContent() : Results.NotFound();
    })
      .WithName("DeleteEmergencyContactV1")
      .WithSummary("Delete emergency contact")
      .WithDescription("Deletes an emergency contact for the authenticated user.")
      .Produces(StatusCodes.Status204NoContent)
      .Produces(StatusCodes.Status401Unauthorized)
      .Produces(StatusCodes.Status404NotFound);
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
