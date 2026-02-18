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
  .WithEnvironment("SERVICES", "s3,ses")
  .WithEnvironment("DEFAULT_REGION", "ap-south-1")
  .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
  .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
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
  .WaitFor(postgres)
  .WaitFor(redis)
  .WaitFor(localStack);
#pragma warning restore S5332
#pragma warning restore S2068

builder.Build().Run();
