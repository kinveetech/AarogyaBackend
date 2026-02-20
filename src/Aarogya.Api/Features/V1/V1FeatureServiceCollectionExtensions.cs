using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.Features.V1.Users;

namespace Aarogya.Api.Features.V1;

internal static class V1FeatureServiceCollectionExtensions
{
  public static IServiceCollection AddV1FeatureServices(this IServiceCollection services)
  {
    services.AddSingleton<IUserProfileService, InMemoryUserProfileService>();
    services.AddSingleton<IReportService, InMemoryReportService>();
    services.AddScoped<IReportFileUploadService, S3ReportFileUploadService>();
    services.AddScoped<IReportChecksumVerificationService, S3ReportChecksumVerificationService>();
    services.AddSingleton<IAccessGrantService, InMemoryAccessGrantService>();
    services.AddSingleton<IEmergencyContactService, InMemoryEmergencyContactService>();

    return services;
  }
}
