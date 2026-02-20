using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.Features.V1.Users;

namespace Aarogya.Api.Features.V1;

internal static class V1FeatureServiceCollectionExtensions
{
  public static IServiceCollection AddV1FeatureServices(this IServiceCollection services)
  {
    services.AddScoped<IUserProfileService, UserProfileService>();
    services.AddScoped<IReportService, ReportService>();
    services.AddScoped<IReportFileUploadService, S3ReportFileUploadService>();
    services.AddScoped<IReportChecksumVerificationService, S3ReportChecksumVerificationService>();
    services.AddScoped<IPatientNotificationService, LoggingPatientNotificationService>();
    services.AddHostedService<S3UploadNotificationConfiguratorHostedService>();
    services.AddHostedService<S3UploadEventConsumerHostedService>();
    services.AddScoped<IAccessGrantService, AccessGrantService>();
    services.AddSingleton<IEmergencyContactService, InMemoryEmergencyContactService>();

    return services;
  }
}
