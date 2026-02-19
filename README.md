# Aarogya Backend API

ASP.NET Core 9.0 REST API following Clean Architecture principles.

## 🏗️ Architecture

```
src/
├── Aarogya.Api/            # Web API Layer
│   ├── Controllers/        # API endpoints
│   ├── Middleware/         # Custom middleware
│   └── Program.cs          # Application startup
├── Aarogya.Domain/         # Domain Layer
│   ├── Entities/           # Domain models
│   ├── Interfaces/         # Contracts
│   └── Services/           # Business logic
└── Aarogya.Infrastructure/ # Data & External Services
    ├── Data/               # EF Core context
    ├── Repositories/       # Data access
    └── Services/           # External integrations

tests/
├── Aarogya.Api.Tests/      # API integration tests
└── Aarogya.Domain.Tests/   # Unit tests
```

## ⚙️ Current Service Setup

| Mode | Services | Access |
|------|----------|--------|
| Local .NET | `Aarogya.Api` only | `http://localhost:5000` / `https://localhost:5001` |
| Docker Compose | `aarogya-api`, `aarogya-postgres`, `aarogya-redis`, `aarogya-localstack`, `aarogya-pgadmin` | API: `http://localhost:8080/swagger/index.html`, LocalStack: `http://localhost:4566/_localstack/health`, pgAdmin: `http://localhost:5050` |
| .NET Aspire AppHost | `api`, `postgres`, `redis`, `localstack` | `dotnet run --project AppHost` (dashboard URL shown in console) |
| Kubernetes (`kind`) | `aarogya-api`, `postgres`, `redis`, `pgadmin` in namespace `aarogya` | API: `kubectl -n aarogya port-forward svc/aarogya-api 8080:80`, pgAdmin: `kubectl -n aarogya port-forward svc/pgadmin 5050:80` |

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- .NET Aspire workload (`dotnet workload install aspire`)
- Docker Desktop
- `kubectl` (for Kubernetes setup)
- `kind` (for local Kubernetes setup)
- Visual Studio 2022 / VS Code / Rider

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd backend
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Update connection string**

   Edit `src/Aarogya.Api/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=aarogya;Username=aarogya;Password=aarogya_dev_password"
     }
   }
   ```

4. **Run database migrations**
   ```bash
   dotnet tool restore
   dotnet ef database update --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Infrastructure --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/
   ```

5. **Run the application**
   ```bash
   dotnet run --project src/Aarogya.Api
   ```

   API will be available at:
   - HTTPS: `https://localhost:5001`
   - HTTP: `http://localhost:5000`
   - Swagger UI: `https://localhost:5001/swagger`
   - Health (all checks): `https://localhost:5001/health`
   - Health (readiness): `https://localhost:5001/health/ready`

## 🧪 Dev Container

This repository includes a VS Code Dev Container setup in `.devcontainer/`.

What it provides:
- .NET SDK (aligned with this repo's `.NET 9` setup)
- AWS CLI
- PostgreSQL client (`psql`)
- C# Dev Kit extensions preinstalled

Use in VS Code:
1. Open the repository
2. Run `Dev Containers: Reopen in Container`
3. Wait for post-create setup to complete

The dev container uses `.devcontainer/docker-compose.yml` and includes a local PostgreSQL service for development workflows.

## 📡 API Endpoints

### Authentication
```
POST   /api/auth/register          Register new user
POST   /api/auth/login             User login
POST   /api/auth/refresh           Refresh JWT token
POST   /api/auth/logout            User logout
POST   /api/auth/forgot-password   Password reset request
POST   /api/auth/reset-password    Complete password reset
```

### User Management
```
GET    /api/users/me               Get current user
PUT    /api/users/me               Update user profile
DELETE /api/users/me               Delete account
POST   /api/users/me/avatar        Upload avatar
```

## 🔐 Authentication

This API uses JWT Bearer token authentication.

### Getting a Token
```bash
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "YourPassword123!"
}
```

### Using the Token
```bash
GET /api/users/me
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Token Configuration
Edit `appsettings.json`:
```json
{
  "Jwt": {
    "Key": "your-secret-key-minimum-32-characters",
    "Issuer": "MobileAppAPI",
    "Audience": "MobileAppClients",
    "ExpiryInMinutes": 60
  }
}
```

### Cognito Configuration
Configure Cognito under the `Aws:Cognito` section:

```json
{
  "Aws": {
    "Cognito": {
      "UserPoolName": "aarogya-dev-users",
      "UserPoolId": "SET_VIA_ENV_VAR",
      "AppClientId": "SET_VIA_ENV_VAR",
      "MfaConfiguration": "OPTIONAL",
      "PasswordPolicy": {
        "MinimumLength": 8,
        "RequireLowercase": true,
        "RequireUppercase": true,
        "RequireNumbers": true,
        "RequireSymbols": true
      }
    }
  }
}
```

Notes:
- `MfaConfiguration` supports `OFF`, `ON`, or `OPTIONAL`.
- LocalStack bootstrap attempts to create/update the user pool with email + phone sign-up attributes.
- In local/dev, these values can be overridden via `.env` (`AWS_COGNITO_*`) or environment variables.

### PII Encryption Configuration
PII fields (`first_name`, `last_name`, `email`, `phone`, emergency-contact `name/phone`) are encrypted at the application layer and stored in `bytea` columns. Blind indexes are generated for searchable fields (`email_hash`, `phone_hash`).

Configure encryption via `Encryption` settings:
```json
{
  "Encryption": {
    "UseAwsKms": true,
    "KmsKeyId": "alias/aarogya-prod-data-key",
    "LocalDataKey": "dev-only-local-fallback-key",
    "BlindIndexKey": "hmac-secret-for-blind-indexes"
  }
}
```

Notes:
- Production: keep `UseAwsKms=true` and set `KmsKeyId`.
- Local/dev: set `UseAwsKms=false` and provide `LocalDataKey`.
- Always set a strong `BlindIndexKey` via user-secrets or environment variables.

### Aadhaar Vault Configuration
Issue #19 introduces a dedicated Aadhaar vault schema with:
- `aadhaar_vault.aadhaar_records` for encrypted Aadhaar + SHA-256 lookup + reference token
- `aadhaar_vault.access_audit_logs` for separate vault access auditing

Configure mock Aadhaar API endpoints:
```json
{
  "AadhaarVault": {
    "UseMockApi": true,
    "MockApiBaseUrl": "http://localhost:5099",
    "ValidateEndpoint": "/api/mock/uidai/validate",
    "TokenizeEndpoint": "/api/mock/uidai/tokenize"
  }
}
```

### Seed Data (Development/Test)
Seed generation is idempotent and faker-based. It creates realistic fake users, reports, report parameters, access grants, emergency contacts, audit logs, and Aadhaar vault records.

Configuration:
```json
{
  "SeedData": {
    "EnableOnStartup": true,
    "PatientsCount": 24,
    "DoctorsCount": 6,
    "LabTechniciansCount": 4,
    "AdminsCount": 1,
    "ReportsPerPatient": 3
  }
}
```

Notes:
- `EnableOnStartup` is `true` in Development and `false` in base config.
- Seeding skips when seed users already exist (`external_auth_id` prefixed with `seed-`), so it is safe to re-run.

## 🧪 Testing

### Run all tests
```bash
dotnet test
```

### Run with coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run specific test project
```bash
dotnet test tests/Aarogya.Api.Tests
```

## 🔧 Development Tools

### Code Formatting
```bash
dotnet format
```

### Database Migrations

Restore local tools first:
```bash
dotnet tool restore
```

Create migration:
```bash
dotnet ef migrations add MigrationName --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Infrastructure --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/
```

Apply migration:
```bash
dotnet ef database update --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Infrastructure --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/
```

Remove last migration:
```bash
dotnet ef migrations remove --project src/Aarogya.Infrastructure --startup-project src/Aarogya.Infrastructure --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/
```

JSONB index verification:
```bash
psql "<connection-string>" -f docs/database/jsonb-index-verification.sql
```

## 📦 Dependencies

### API Layer
- Microsoft.AspNetCore.Authentication.JwtBearer
- Swashbuckle.AspNetCore (Swagger)
- Serilog.AspNetCore
- FluentValidation.AspNetCore

### Infrastructure Layer
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.SqlServer
- Npgsql.EntityFrameworkCore.PostgreSQL

### Data Access Abstractions
- Generic repository contract: `IRepository<T>`
- Unit of work contract: `IUnitOfWork`
- Specification pattern contracts: `ISpecification<T>` + `BaseSpecification<T>`
- EF Core implementations live under `src/Aarogya.Infrastructure/Persistence/Repositories`

## 🐳 Local Docker Run

### Docker Compose (API + PostgreSQL + Redis + LocalStack + pgAdmin)
```bash
docker compose up --build -d
docker compose ps
curl http://localhost:8080/swagger/index.html
```

Compose services:
- API: `aarogya-api` (`8080`)
- PostgreSQL 16 + `pgcrypto`: `aarogya-postgres` (`5432`)
- PostgreSQL extension bootstrap: `aarogya-postgres-init` (one-shot `CREATE EXTENSION IF NOT EXISTS pgcrypto`)
- Redis 7: `aarogya-redis` (`6379`)
- LocalStack 3: `aarogya-localstack` (`4566`) with init script for S3/SQS/Cognito/KMS
- pgAdmin: `aarogya-pgadmin` (`5050`)

pgAdmin default login:
- Email: `admin@aarogya.com`
- Password: `admin`

Environment configuration:
- Copy `.env.example` to `.env` and customize values as needed.
- `docker-compose.yml` reads DB, pgAdmin, Redis, JWT, and AWS/Cognito settings from `.env` with safe defaults.
- Cognito-specific toggles include: `AWS_COGNITO_USER_POOL_NAME`, `AWS_COGNITO_MFA_CONFIGURATION`, `AWS_COGNITO_PASSWORD_MIN_LENGTH`, and `AWS_COGNITO_PASSWORD_REQUIRE_*`.

Named volumes used for persistence:
- `aarogyabackend_pgdata` (PostgreSQL data)
- `aarogyabackend_redisdata` (Redis AOF/persistence data)
- `aarogyabackend_localstackdata` (LocalStack state)
- `aarogyabackend_pgadmindata` (pgAdmin settings and state)

Useful commands:
```bash
docker compose logs -f api
curl http://localhost:4566/_localstack/health
docker compose exec localstack aws --endpoint-url http://localhost:4566 s3api list-buckets
docker compose exec localstack aws --endpoint-url http://localhost:4566 sqs list-queues
docker compose exec localstack aws --endpoint-url http://localhost:4566 kms list-aliases
docker compose exec localstack aws --endpoint-url http://localhost:4566 cognito-idp list-user-pools --max-results 60
docker compose down -v
```

LocalStack note:
- `cognito-idp` calls may require LocalStack Pro depending on your image/license.
- This repo's init script attempts Cognito provisioning and logs a warning when the API is unavailable.

### .NET Aspire AppHost (API + PostgreSQL + Redis + LocalStack)
```bash
dotnet run --project AppHost
```

AppHost services:
- `api` (Aarogya API)
- `postgres` (PostgreSQL 16)
- `redis` (Redis 7)
- `localstack` (S3 + SQS + Cognito + KMS + SES emulation)

Aspire dashboard:
- URL is printed in AppHost startup logs
- Default HTTP profile is `http://localhost:15236`

To stop running AppHost instances:
```bash
# If AppHost is running in current terminal
# Press Ctrl+C

# If running in another terminal/background
pkill -f "AppHost/AppHost.csproj" || true
pkill -f "AppHost.dll" || true

# Clean up AppHost-created containers
docker ps -a --format '{{.Names}}' | grep -E '^(postgres|redis|localstack|api)-' | xargs -r docker rm -f
```

### Docker only (API)
```bash
docker build -t aarogya-api:dev .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e Jwt__Key=development-key-change-this-to-32-plus-chars \
  aarogya-api:dev
```

## ☸️ Local Kubernetes Run

### 1. Create cluster (first time only)
```bash
kind create cluster --name aarogya-backend
kubectl config use-context kind-aarogya-backend
```

### 2. Build image
```bash
docker build -t aarogya-api:dev .
```

### 3. Load image into your local cluster
For `kind` (cluster created above):
```bash
kind load docker-image aarogya-api:dev --name aarogya-backend
```

For `minikube`:
```bash
minikube image load aarogya-api:dev
```

### 4. Apply manifests
```bash
kubectl apply -k k8s
kubectl -n aarogya get pods
kubectl -n aarogya get svc
```

### 5. Access API
```bash
kubectl -n aarogya port-forward svc/aarogya-api 8080:80
```

Then open `http://localhost:8080/swagger/index.html`.

To access pgAdmin in Kubernetes:
```bash
kubectl -n aarogya port-forward svc/pgadmin 5050:80
```

Then open `http://localhost:5050` with:
- Email: `admin@aarogya.com`
- Password: `admin`

If using `k9s`, switch namespace to `aarogya` to view these pods.

## 🩺 Health Checks

Health checks are configured via ASP.NET Core health check middleware.

Endpoints:
- `/health` (all checks)
- `/health/ready` (checks tagged as `ready`)

Database health check:
- Name: `postgresql`
- Includes a configurable timeout via `Database:HealthCheckTimeoutSeconds` in:
  - `src/Aarogya.Api/appsettings.json`
  - `src/Aarogya.Api/appsettings.Development.json`

## 🧰 Redis Cache Configuration

Redis settings are configured in:
- `src/Aarogya.Api/appsettings.json`
- `src/Aarogya.Api/appsettings.Development.json`

Core keys:
- `ConnectionStrings:Redis`
- `Redis:InstanceName`
- `Redis:Database`
- `Redis:ConnectTimeoutMilliseconds`
- `Redis:ConnectRetry`
- `Redis:SyncTimeoutMilliseconds`
- `Redis:DefaultExpirationMinutes`
- `Redis:KeyPrefix`

For local secrets, use `src/Aarogya.Api/secrets.template.json` and set with:
```bash
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379,abortConnect=false,connectTimeout=5000" --project src/Aarogya.Api
```

Redis key naming convention:
- Format: `<Redis:KeyPrefix>:<bounded-context>:<resource>:<id>`
- Development example: `aarogya:dev:reports:12345`
- Production example: `aarogya:prod:reports:12345`

## 📊 Logging

This project uses Serilog for structured logging.

Environment-specific logging configuration files:
- `src/Aarogya.Api/appsettings.Development.json`
- `src/Aarogya.Api/appsettings.Staging.json`
- `src/Aarogya.Api/appsettings.Production.json`

Logging profile:
- Development: `Debug` default level, console sink
- Staging: `Information` default level, console + rolling file sink
- Production: `Information` default level, console + rolling file sink

Request logging:
- Enabled via `UseSerilogRequestLogging()` in `src/Aarogya.Api/Program.cs`
- Logs method, path, status code, elapsed time, trace id, and client IP
- Does not log `Authorization` or `Cookie` values; only records if sensitive headers are present

Log retention:
- Staging file logs keep 14 rolling daily files
- Production file logs keep 30 rolling daily files

## 🔍 API Documentation

Once running, visit Swagger UI:
- Development: `https://localhost:5001/swagger`
- Production: `https://your-domain.com/swagger` (if enabled)

## ✅ PR Quality Gates

Current PR checks:
- `.NET Backend CI / build-and-test`
- `.NET Backend CI / lint`
- `PR Guardrails / semantic-pr-title`
- `PR Guardrails / dependency-review-disabled` (or `dependency-review` when enabled)

See `/docs/github-main-guardrails.md` for full guardrail and ruleset setup details.

## 🔒 Configuration Security

Configuration validation runs at startup and enforces:
- required keys (`ConnectionStrings:DefaultConnection`, `Jwt:Key`)
- secure JWT key length
- secure connection string values (non-default credentials in non-development environments)
- valid URL formats for configurable endpoints (for example `Aws:ServiceUrl`, CORS origins)

Sensitive configuration guidance:
- keep secrets in user-secrets or environment variables, not committed JSON files
- use placeholder values only in templates (`secrets.template.json`, `.env.example`)
- prefer `AAROGYA_`-prefixed environment variables in deployed environments

Install local git hooks to prevent accidental secret commits:
```bash
./scripts/install-git-hooks.sh
```

The pre-commit hook scans staged files for common secret patterns and blocks unsafe commits.

## 🐛 Troubleshooting

### Port already in use
Change ports in `src/Aarogya.Api/Properties/launchSettings.json`

### Database connection fails
1. For Docker: verify `aarogya-postgres` is healthy (`docker compose ps`)
2. Verify `aarogya-redis` is healthy if Redis-backed features are enabled
3. For Kubernetes: verify `postgres` pod is running in namespace `aarogya`
4. Check `ConnectionStrings__DefaultConnection` override in your runtime environment

### JWT token errors
Ensure JWT:Key in appsettings.json is at least 32 characters

## 📝 Code Style

This project follows:
- Clean Architecture principles
- SOLID principles
- RESTful API conventions
- Async/await patterns
- Repository pattern

## 🤝 Contributing

1. Create feature branch: `git checkout -b feature/amazing-feature`
2. Commit changes: `git commit -m 'feat: add amazing feature'`
3. Push branch: `git push origin feature/amazing-feature`
4. Open Pull Request

## 📄 License

This project is licensed under the MIT License.
