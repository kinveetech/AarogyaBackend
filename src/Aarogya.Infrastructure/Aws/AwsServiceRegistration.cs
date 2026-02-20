using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.NETCore.Setup;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Amazon.SimpleEmailV2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aarogya.Infrastructure.Aws;

public static class AwsServiceRegistration
{
  public static IServiceCollection AddAwsServices(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var awsSection = configuration.GetSection("Aws");
    var region = awsSection["Region"] ?? "ap-south-1";
    var serviceUrl = awsSection["ServiceUrl"];
    var useLocalStack = awsSection.GetSection("UseLocalStack").Get<bool?>() ?? false;
    var accessKey = awsSection["AccessKey"] ?? string.Empty;
    var secretKey = awsSection["SecretKey"] ?? string.Empty;

    var awsOptions = new AWSOptions
    {
      Region = RegionEndpoint.GetBySystemName(region)
    };

    // Use explicit credentials when provided (LocalStack or non-IAM environments)
    if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
    {
      awsOptions.Credentials = new BasicAWSCredentials(accessKey, secretKey);
    }

    services.AddDefaultAWSOptions(awsOptions);

    // Register S3 client
    if (useLocalStack && !string.IsNullOrWhiteSpace(serviceUrl))
    {
      services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonS3Config
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          ForcePathStyle = true
        }));
    }
    else
    {
      services.AddAWSService<IAmazonS3>();
    }

    // Register SES v2 client
    if (useLocalStack && !string.IsNullOrWhiteSpace(serviceUrl))
    {
      services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ => new AmazonSimpleEmailServiceV2Client(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonSimpleEmailServiceV2Config
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl
        }));
    }
    else
    {
      services.AddAWSService<IAmazonSimpleEmailServiceV2>();
    }

    // Register SQS client
    if (useLocalStack && !string.IsNullOrWhiteSpace(serviceUrl))
    {
      services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonSQSConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl
        }));
    }
    else
    {
      services.AddAWSService<IAmazonSQS>();
    }

    // Register AWS KMS client
    if (useLocalStack && !string.IsNullOrWhiteSpace(serviceUrl))
    {
      services.AddSingleton<IAmazonKeyManagementService>(_ => new AmazonKeyManagementServiceClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonKeyManagementServiceConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl
        }));
    }
    else
    {
      services.AddAWSService<IAmazonKeyManagementService>();
    }

    // Register Cognito Identity Provider client
    if (useLocalStack && !string.IsNullOrWhiteSpace(serviceUrl))
    {
      services.AddSingleton<IAmazonCognitoIdentityProvider>(_ => new AmazonCognitoIdentityProviderClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonCognitoIdentityProviderConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl
        }));
    }
    else
    {
      services.AddAWSService<IAmazonCognitoIdentityProvider>();
    }

    return services;
  }
}
