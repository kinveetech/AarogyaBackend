var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
  .WithImageTag("16");

var db = postgres.AddDatabase("DefaultConnection", "aarogya");

var redis = builder.AddRedis("Redis")
  .WithImageTag("7");

var localStack = builder.AddContainer("localstack", "localstack/localstack")
  .WithImageTag("3")
  .WithEnvironment("SERVICES", "s3,sqs,cognito,kms,ses")
  .WithEnvironment("AWS_DEFAULT_REGION", "ap-south-1")
  .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
  .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
  .WithEnvironment("LOCALSTACK_S3_BUCKET_NAME", "aarogya-dev")
  .WithEnvironment("LOCALSTACK_SQS_QUEUE_NAME", "aarogya-dev-queue")
  .WithEnvironment("LOCALSTACK_KMS_ALIAS", "alias/aarogya-dev")
  .WithEnvironment("LOCALSTACK_COGNITO_USER_POOL_NAME", "aarogya-dev-users")
  .WithEnvironment("LOCALSTACK_COGNITO_MFA_CONFIGURATION", "OPTIONAL")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_MIN_LENGTH", "8")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_LOWERCASE", "true")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_UPPERCASE", "true")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_NUMBERS", "true")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_SYMBOLS", "true")
  .WithEndpoint(name: "http", port: 4566, targetPort: 4566);

var localStackEndpoint = localStack.GetEndpoint("http");

#pragma warning disable S5332 // LocalStack edge endpoint uses HTTP for local emulation.
builder.AddProject("api", "../src/Aarogya.Api/Aarogya.Api.csproj")
  .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
  .WithReference(db)
  .WithReference(redis)
  .WithEnvironment("Aws__UseLocalStack", "true")
  .WithEnvironment("Aws__ServiceUrl",
    ReferenceExpression.Create($"http://{localStackEndpoint.Property(EndpointProperty.Host)}:{localStackEndpoint.Property(EndpointProperty.Port)}"))
  .WithEnvironment("Aws__AccessKey", "test")
  .WithEnvironment("Aws__SecretKey", "test")
  .WithEnvironment("Aws__S3__BucketName", "aarogya-dev")
  .WithEnvironment("Aws__Sqs__QueueName", "aarogya-dev-queue")
  .WithEnvironment("Aws__S3__PresignedUrlExpiryMinutes", "15")
  .WithEnvironment("Aws__S3__CloudFront__Enabled", "false")
  .WithEnvironment("Aws__Cognito__UserPoolName", "aarogya-dev-users")
  .WithEnvironment("Aws__Cognito__UserPoolId", "SET_VIA_USER_SECRETS_OR_ENV_VAR")
  .WithEnvironment("Aws__Cognito__AppClientId", "SET_VIA_USER_SECRETS_OR_ENV_VAR")
  .WithEnvironment("Aws__Cognito__Issuer",
    ReferenceExpression.Create($"http://{localStackEndpoint.Property(EndpointProperty.Host)}:{localStackEndpoint.Property(EndpointProperty.Port)}/SET_VIA_USER_SECRETS_OR_ENV_VAR"))
  .WithEnvironment("Aws__Cognito__MfaConfiguration", "OPTIONAL")
  .WithEnvironment("Aws__Cognito__PasswordPolicy__MinimumLength", "8")
  .WithEnvironment("Aws__Cognito__PasswordPolicy__RequireLowercase", "true")
  .WithEnvironment("Aws__Cognito__PasswordPolicy__RequireUppercase", "true")
  .WithEnvironment("Aws__Cognito__PasswordPolicy__RequireNumbers", "true")
  .WithEnvironment("Aws__Cognito__PasswordPolicy__RequireSymbols", "true")
  .WaitFor(postgres)
  .WaitFor(redis)
  .WaitFor(localStack);
#pragma warning restore S5332

builder.Build().Run();
