var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable S2068 // Dev-only container bootstrap value, not a production credential.
var postgres = builder.AddContainer("postgres", "postgres")
  .WithImageTag("16")
  .WithEnvironment("POSTGRES_DB", "aarogya")
  .WithEnvironment("POSTGRES_USER", "aarogya")
  .WithEnvironment("POSTGRES_PASSWORD", "aarogya_dev_password")
  .WithEndpoint(name: "tcp", port: 5432, targetPort: 5432);

var redis = builder.AddContainer("redis", "redis")
  .WithImageTag("7")
  .WithArgs("--appendonly", "yes")
  .WithEndpoint(name: "tcp", port: 6379, targetPort: 6379);

var localStack = builder.AddContainer("localstack", "localstack/localstack")
  .WithImageTag("3")
  .WithEnvironment("SERVICES", "s3,sqs,cognito,kms,ses")
  .WithEnvironment("AWS_DEFAULT_REGION", "ap-south-1")
  .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
  .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
  .WithEnvironment("LOCALSTACK_S3_BUCKET_NAME", "aarogya-dev")
  .WithEnvironment("LOCALSTACK_KMS_ALIAS", "alias/aarogya-dev")
  .WithEnvironment("LOCALSTACK_COGNITO_USER_POOL_NAME", "aarogya-dev-users")
  .WithEnvironment("LOCALSTACK_COGNITO_MFA_CONFIGURATION", "OPTIONAL")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_MIN_LENGTH", "8")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_LOWERCASE", "true")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_UPPERCASE", "true")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_NUMBERS", "true")
  .WithEnvironment("LOCALSTACK_COGNITO_PASSWORD_REQUIRE_SYMBOLS", "true")
  .WithEndpoint(name: "http", port: 4566, targetPort: 4566);

#pragma warning disable S5332 // LocalStack edge endpoint uses HTTP for local emulation.
builder.AddProject("api", "../src/Aarogya.Api/Aarogya.Api.csproj")
  .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
  .WithEnvironment(
    "ConnectionStrings__DefaultConnection",
    "Host=postgres;Port=5432;Database=aarogya;Username=aarogya;Password=aarogya_dev_password")
  .WithEnvironment(
    "ConnectionStrings__Redis",
    "redis:6379,abortConnect=false,connectTimeout=5000")
  .WithEnvironment("Aws__UseLocalStack", "true")
  .WithEnvironment("Aws__ServiceUrl", "http://localstack:4566")
  .WithEnvironment("Aws__AccessKey", "test")
  .WithEnvironment("Aws__SecretKey", "test")
  .WithEnvironment("Aws__S3__BucketName", "aarogya-dev")
  .WithEnvironment("Aws__S3__PresignedUrlExpiryMinutes", "15")
  .WithEnvironment("Aws__S3__CloudFront__Enabled", "false")
  .WithEnvironment("Aws__Cognito__UserPoolName", "aarogya-dev-users")
  .WithEnvironment("Aws__Cognito__UserPoolId", "SET_VIA_USER_SECRETS_OR_ENV_VAR")
  .WithEnvironment("Aws__Cognito__AppClientId", "SET_VIA_USER_SECRETS_OR_ENV_VAR")
  .WithEnvironment("Aws__Cognito__Issuer", "http://localstack:4566/SET_VIA_USER_SECRETS_OR_ENV_VAR")
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
#pragma warning restore S2068

builder.Build().Run();
