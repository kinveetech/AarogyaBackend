using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.CloudFront;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.NETCore.Setup;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleEmailV2;
using Amazon.SQS;
using Amazon.Textract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aarogya.Infrastructure.Aws;

public static class AwsServiceRegistration
{
  [SuppressMessage(
    "Maintainability",
    "CA1502:Avoid excessive complexity",
    Justification = "Service registration branches are explicit by AWS client type and environment mode.")]
  public static IServiceCollection AddAwsServices(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var awsSection = configuration.GetSection("Aws");
    var region = awsSection["Region"] ?? "ap-south-1";
    var serviceUrl = awsSection["ServiceUrl"];
    var useLocalStack = awsSection.GetSection("UseLocalStack").Get<bool?>() ?? false;
    var enforceTls13 = configuration.GetSection("TransportSecurity").GetSection("EnforceTls13").Get<bool?>() ?? false;
    var accessKey = awsSection["AccessKey"] ?? string.Empty;
    var secretKey = awsSection["SecretKey"] ?? string.Empty;
    var hasServiceUrl = !string.IsNullOrWhiteSpace(serviceUrl);

    if (enforceTls13 && !useLocalStack && hasServiceUrl && !serviceUrl!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException("Aws:ServiceUrl must use HTTPS when TransportSecurity:EnforceTls13=true.");
    }

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
    if (useLocalStack && hasServiceUrl)
    {
      var useHttp = serviceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
      services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonS3Config
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          ForcePathStyle = true,
          UseHttp = useHttp
        }));
    }
    else
    {
      services.AddAWSService<IAmazonS3>();
    }

    // Register CloudFront client
    if (useLocalStack && hasServiceUrl)
    {
      var useHttp = serviceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
      services.AddSingleton<IAmazonCloudFront>(_ => new AmazonCloudFrontClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonCloudFrontConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          UseHttp = useHttp
        }));
    }
    else
    {
      services.AddAWSService<IAmazonCloudFront>();
    }

    // Register SES v2 client
    if (useLocalStack && hasServiceUrl)
    {
      var useHttp = serviceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
      services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ => new AmazonSimpleEmailServiceV2Client(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonSimpleEmailServiceV2Config
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          UseHttp = useHttp
        }));
    }
    else
    {
      services.AddAWSService<IAmazonSimpleEmailServiceV2>();
    }

    // Register SQS client
    if (useLocalStack && hasServiceUrl)
    {
      var useHttp = serviceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
      services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonSQSConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          UseHttp = useHttp
        }));
    }
    else
    {
      services.AddAWSService<IAmazonSQS>();
    }

    // Register AWS KMS client
    if (useLocalStack && hasServiceUrl)
    {
      var useHttp = serviceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
      services.AddSingleton<IAmazonKeyManagementService>(_ => new AmazonKeyManagementServiceClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonKeyManagementServiceConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          UseHttp = useHttp
        }));
    }
    else
    {
      services.AddAWSService<IAmazonKeyManagementService>();
    }

    // Register Bedrock Runtime client (not available in LocalStack)
    if (!useLocalStack)
    {
      var bedrockRegion = configuration.GetSection("PdfExtraction")["BedrockRegion"];
      if (!string.IsNullOrWhiteSpace(bedrockRegion))
      {
#pragma warning disable CS0618 // FallbackCredentialsFactory is deprecated but remains functional
        services.AddSingleton<IAmazonBedrockRuntime>(_ => new AmazonBedrockRuntimeClient(
          awsOptions.Credentials ?? FallbackCredentialsFactory.GetCredentials(),
          RegionEndpoint.GetBySystemName(bedrockRegion)));
#pragma warning restore CS0618
      }
      else
      {
        services.AddAWSService<IAmazonBedrockRuntime>();
      }
    }

    // Register Textract client
    if (useLocalStack && hasServiceUrl)
    {
      var useHttp = serviceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
      services.AddSingleton<IAmazonTextract>(_ => new AmazonTextractClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonTextractConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          UseHttp = useHttp
        }));
    }
    else
    {
      services.AddAWSService<IAmazonTextract>();
    }

    // Register Cognito Identity Provider client
    if (useLocalStack && hasServiceUrl)
    {
      var useHttp = serviceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
      services.AddSingleton<IAmazonCognitoIdentityProvider>(_ => new AmazonCognitoIdentityProviderClient(
        awsOptions.Credentials ?? new BasicAWSCredentials("test", "test"),
        new AmazonCognitoIdentityProviderConfig
        {
          RegionEndpoint = awsOptions.Region,
          ServiceURL = serviceUrl,
          UseHttp = useHttp
        }));
    }
    else
    {
      services.AddAWSService<IAmazonCognitoIdentityProvider>();
    }

    return services;
  }
}
