using Microsoft.AspNetCore.Authentication;

namespace Aarogya.Api.Authorization;

internal static class AarogyaAuthorizationExtensions
{
  public static IServiceCollection AddAarogyaAuthorization(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddAuthorization(options =>
    {
      options.AddPolicy(AarogyaPolicies.AnyRegisteredRole, policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, AarogyaRoles.All));

      options.AddPolicy(AarogyaPolicies.Patient, policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, AarogyaRoles.Patient));

      options.AddPolicy(AarogyaPolicies.Doctor, policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, AarogyaRoles.Doctor));

      options.AddPolicy(AarogyaPolicies.LabTechnician, policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, AarogyaRoles.LabTechnician));

      options.AddPolicy(AarogyaPolicies.Admin, policy =>
        policy.RequireClaim(System.Security.Claims.ClaimTypes.Role, AarogyaRoles.Admin));
    });

    services.AddTransient<IClaimsTransformation, AarogyaRoleClaimsTransformation>();
    return services;
  }
}
