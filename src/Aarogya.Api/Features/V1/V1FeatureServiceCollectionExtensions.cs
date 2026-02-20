using Aarogya.Api.Caching;
using Aarogya.Api.Features.V1.AccessGrants;
using Aarogya.Api.Features.V1.Consents;
using Aarogya.Api.Features.V1.EmergencyAccess;
using Aarogya.Api.Features.V1.EmergencyContacts;
using Aarogya.Api.Features.V1.Notifications;
using Aarogya.Api.Features.V1.Reports;
using Aarogya.Api.Features.V1.Users;

namespace Aarogya.Api.Features.V1;

internal static class V1FeatureServiceCollectionExtensions
{
  public static IServiceCollection AddV1FeatureServices(this IServiceCollection services)
  {
    services.AddSingleton<IEntityCacheService, DistributedEntityCacheService>();
    services.AddScoped<UserProfileService>();
    services.AddScoped<IUserProfileService, CachedUserProfileService>();
    services.AddScoped<IUserDataRightsService, UserDataRightsService>();
    services.AddScoped<ReportService>();
    services.AddScoped<IReportService, CachedReportService>();
    services.AddScoped<ICloudFrontInvalidationService, CloudFrontInvalidationService>();
    services.AddScoped<IReportFileUploadService, S3ReportFileUploadService>();
    services.AddScoped<IReportChecksumVerificationService, S3ReportChecksumVerificationService>();
    services.AddScoped<IReportVirusScanProcessor, ReportVirusScanProcessor>();
    services.AddSingleton<IReportVirusScanner, ClamAvReportVirusScanner>();
    services.AddSingleton<INotificationPreferenceService, InMemoryNotificationPreferenceService>();
    services.AddScoped<ITransactionalEmailNotificationService, TransactionalEmailNotificationService>();
    services.AddScoped<ITransactionalEmailSender, SesTransactionalEmailSender>();
    services.AddScoped<ICriticalSmsNotificationService, CriticalSmsNotificationService>();
    services.AddScoped<IPatientNotificationService, LoggingPatientNotificationService>();
    services.AddSingleton<IDeviceTokenRegistry, InMemoryDeviceTokenRegistry>();
    services.AddScoped<IPushNotificationService, PushNotificationService>();
    services.AddHttpClient<IPushNotificationSender, FirebasePushNotificationSender>(client =>
    {
      client.Timeout = TimeSpan.FromSeconds(10);
    });
    services.AddHostedService<S3UploadNotificationConfiguratorHostedService>();
    services.AddHostedService<S3UploadEventConsumerHostedService>();
    services.AddHostedService<ClamAvDefinitionsUpdaterHostedService>();
    services.AddHostedService<ReportHardDeleteHostedService>();
    services.AddHostedService<EmergencyAccessExpiryHostedService>();
    services.AddScoped<AccessGrantService>();
    services.AddScoped<IAccessGrantService, CachedAccessGrantService>();
    services.AddScoped<IEmergencyAccessService, EmergencyAccessService>();
    services.AddScoped<IEmergencyAccessAuditTrailService, EmergencyAccessAuditTrailService>();
    services.AddScoped<IEmergencyContactService, EmergencyContactService>();
    services.AddScoped<IConsentService, ConsentService>();

    return services;
  }
}
