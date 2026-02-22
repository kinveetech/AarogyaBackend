# Aarogya Backend API
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=kinveetech_AarogyaBackend&metric=alert_status&token=72210914ef9f5e044f998d58cb1a2c472d4c63b1)](https://sonarcloud.io/summary/new_code?id=kinveetech_AarogyaBackend)

ASP.NET Core 9.0 REST API following Clean Architecture principles.

## 🏗️ Architecture

```
AppHost/                    # .NET Aspire orchestrator
src/
├── Aarogya.Api/            # HTTP/API layer
│   ├── Controllers/        # Controller-based endpoints (/api/auth, /api/v1/*)
│   ├── Features/           # V1 feature contracts and services
│   ├── Authentication/     # OTP, PKCE, social auth, API keys
│   ├── Authorization/      # Policies, roles, claims transformation
│   ├── RateLimiting/       # Policy setup + response header middleware
│   ├── Validation/         # FluentValidation validators + error contracts
│   └── Program.cs          # Application startup
├── Aarogya.Domain/         # Domain entities, value objects, repository contracts, specs
└── Aarogya.Infrastructure/ # Persistence, AWS integrations, caching, security, seeding

tests/
├── Aarogya.Api.Tests/          # API tests
├── Aarogya.Domain.Tests/       # Domain tests
└── Aarogya.Infrastructure.Tests/ # Infrastructure tests

infra/
└── aws/
    ├── audit-log-archival/     # AWS policy templates for CloudWatch -> Firehose -> S3 archival
    └── cloudfront-report-cdn/  # CloudFormation template for report CDN (OAI + signed URLs)

scripts/
├── install-git-hooks.sh
├── configure-audit-log-archival.sh # One-time setup script for WS6-3 compliance archival
└── configure-kms-key-rotation.sh   # One-time setup script for WS6-4 KMS key rotation
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
   cd AarogyaBackend
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

### Authentication and Authorization (`/api/auth`)
```
POST   /api/auth/otp/request       Request phone OTP
POST   /api/auth/otp/verify        Verify phone OTP
POST   /api/auth/pkce/authorize    Create PKCE authorization code
POST   /api/auth/pkce/token        Exchange PKCE code for tokens
POST   /api/auth/token/refresh     Refresh tokens
POST   /api/auth/token/revoke      Revoke refresh token
POST   /api/auth/social/authorize  Build social provider authorize URL
POST   /api/auth/social/token      Exchange social auth code for tokens
GET    /api/auth/me                Get authenticated user claims
POST   /api/auth/api-keys/issue    Issue partner API key (Admin)
POST   /api/auth/api-keys/rotate   Rotate partner API key (Admin)
GET    /api/auth/api-keys/me       Resolve API key identity (API key policy)
POST   /api/auth/roles/assign      Assign role to user (Admin)
```

### Versioned API (`/api/v1`)
```
GET    /api/v1/users/me                      Get current user profile
PUT    /api/v1/users/me                      Update current user profile
GET    /api/v1/reports                       List reports for current user
POST   /api/v1/reports                       Create report for current user
DELETE /api/v1/reports/{id}                  Soft delete report file
GET    /api/v1/access-grants                 List grants for current patient
POST   /api/v1/access-grants                 Create access grant (Patient policy)
DELETE /api/v1/access-grants/{grantId}       Revoke access grant
GET    /api/v1/emergency-contacts            List emergency contacts
POST   /api/v1/emergency-contacts            Create emergency contact
DELETE /api/v1/emergency-contacts/{contactId} Delete emergency contact
POST   /api/v1/emergency-access/requests     Request break-glass emergency access
GET    /api/v1/consents                      List current user consent states
PUT    /api/v1/consents/{purpose}            Grant/withdraw consent for a purpose
GET    /api/v1/notifications/preferences     Get notification preferences
PUT    /api/v1/notifications/preferences     Update notification preferences
```

## 🔐 Authentication

Protected endpoints support JWT Bearer tokens (issued from PKCE/social flows). Partner integrations are supported via API key authentication policy for specific routes.

### Getting a Token
```bash
CLIENT_ID="mobile-app-client"
REDIRECT_URI="aarogya://auth/callback"
CODE_CHALLENGE="<s256-code-challenge>"
CODE_VERIFIER="<matching-code-verifier>"

# 1) create authorization code
AUTH_CODE=$(curl -s -X POST http://localhost:5000/api/auth/pkce/authorize \
  -H "Content-Type: application/json" \
  -d "{\"clientId\":\"$CLIENT_ID\",\"redirectUri\":\"$REDIRECT_URI\",\"codeChallenge\":\"$CODE_CHALLENGE\",\"codeChallengeMethod\":\"S256\",\"platform\":\"ios\"}" \
  | jq -r '.authorizationCode')

# 2) exchange for tokens
curl -s -X POST http://localhost:5000/api/auth/pkce/token \
  -H "Content-Type: application/json" \
  -d "{\"clientId\":\"$CLIENT_ID\",\"redirectUri\":\"$REDIRECT_URI\",\"authorizationCode\":\"$AUTH_CODE\",\"codeVerifier\":\"$CODE_VERIFIER\"}"
```

### Using the Token
```bash
curl -H "Authorization: Bearer <access-token>" \
  http://localhost:5000/api/v1/users/me
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
    "ActiveKeyId": "kms-2026",
    "LocalDataKey": "dev-only-local-fallback-key",
    "LegacyLocalDataKeys": [
      {
        "KeyId": "local-2025",
        "Secret": "legacy-local-secret"
      }
    ],
    "BlindIndexKey": "hmac-secret-for-blind-indexes"
  },
  "EncryptionRotation": {
    "EnableBackgroundReEncryption": true,
    "CheckIntervalMinutes": 1440,
    "BatchSize": 250
  }
}
```

Notes:
- Production: keep `UseAwsKms=true` and set `KmsKeyId`.
- Local/dev: set `UseAwsKms=false` and provide `LocalDataKey`.
- Set `ActiveKeyId` to a new value during rotation waves (for example `kms-2027`).
- Keep previous local keys in `LegacyLocalDataKeys` for backward decryption during migration.
- A background worker re-encrypts records in batches and writes audit events under `encryption.reencryption.*`.
- Always set a strong `BlindIndexKey` via user-secrets or environment variables.

KMS annual key-rotation bootstrap:
```bash
AWS_REGION=ap-south-1 KMS_ALIAS_NAME=alias/aarogya-prod-data-key ./scripts/configure-kms-key-rotation.sh
```

Detailed runbook: `docs/infrastructure/encryption-key-rotation.md`

### Consent Management (DPDP)
Issue #55 adds consent recording and enforcement for sensitive data workflows.

Supported purposes:
- `profile_management`
- `emergency_contact_management`
- `medical_data_sharing`
- `medical_records_processing`

Behavior:
- Consent is recorded via `PUT /api/v1/consents/{purpose}`.
- The latest consent state per purpose is returned by `GET /api/v1/consents`.
- Protected `/api/v1` endpoints enforce required consent and return `403` when withdrawn or missing.
- Every consent grant/withdraw/denial is written to the audit log trail.

### Virus Scanning Pipeline
Issue #56 adds antivirus scanning for uploaded report files:

- upload flow starts in `processing` state
- S3 upload events are consumed from SQS
- each object is scanned via `IReportVirusScanner` (ClamAV-compatible scanner abstraction)
- clean files transition to `clean`
- infected files are copied to quarantine bucket/prefix and transition to `infected`
- scan metadata is attached to report tags (`scan-status`, `scan-engine`, signature/quarantine key when infected)
- ClamAV definitions refresh runs periodically in a hosted service

Configuration (`appsettings*.json`):
```json
{
  "VirusScanning": {
    "EnableScanning": true,
    "QuarantineBucketName": "aarogya-dev-quarantine",
    "QuarantinePrefix": "quarantine",
    "DefinitionsRefreshIntervalMinutes": 60
  }
}
```

### Report File Deletion and Retention
Issue #57 adds soft-delete and retention-based hard delete for report files:

- `DELETE /api/v1/reports/{id}` marks report as soft-deleted
- soft-deleted reports are excluded from report list/detail/query APIs
- soft-deleted files become unavailable for download/checksum verification APIs
- a background worker hard-deletes the backing S3 object after retention, then clears object key/checksum in DB
- soft-delete actions are written to audit logs (`report.deleted`)

Configuration (`appsettings*.json`):
```json
{
  "FileDeletion": {
    "EnableHardDeleteWorker": true,
    "RetentionDays": 2555,
    "WorkerIntervalMinutes": 60
  }
}
```

### SMS Notification Service (Mock)
Issue #63 adds critical SMS notification flows for:
- OTP delivery (`/api/auth/otp/request`)
- emergency contact lifecycle alerts (create/update/delete)

Current implementation is mock-only (no outbound SNS send), with:
- per-recipient/per-event SMS rate limiting
- per-message estimated cost tracking in structured logs
- Indian-number validation retained (`+91XXXXXXXXXX`)

Configuration (`appsettings*.json`):
```json
{
  "SmsNotifications": {
    "EnableCriticalSms": false,
    "MaxSendsPerWindow": 5,
    "RateLimitWindowSeconds": 60,
    "EstimatedCostPerMessageInInr": 0.25
  }
}
```

### Notification Preferences
Issue #64 adds per-user notification preferences by event and channel.

Supported events:
- `report_uploaded`
- `access_granted`
- `emergency_access`

Supported channels:
- `push`
- `email`
- `sms`

API:
- `GET /api/v1/notifications/preferences`
- `PUT /api/v1/notifications/preferences`

Behavior:
- default preferences are all-enabled on first user interaction
- push/email/sms notification services evaluate preferences before sending

### Emergency Access Request Flow
Issue #65 adds break-glass emergency access requests:
- endpoint: `POST /api/v1/emergency-access/requests`
- only registered emergency contacts (by patient + contact phone match) can trigger requests
- temporary access grant is created for the requested doctor
- patient is notified via push, email, and SMS (subject to notification preferences)
- audit event recorded as `emergency_access.requested`

Configuration (`appsettings*.json`):
```json
{
  "EmergencyAccess": {
    "DefaultDurationHours": 24,
    "MinDurationHours": 24,
    "MaxDurationHours": 48
  }
}
```

### Emergency Access Auto-Expiry
Issue #66 adds a background worker for emergency-access lifecycle:
- sends pre-expiry notifications 1 hour before expiry
- automatically transitions overdue emergency access grants to `Expired`
- writes audit actions: `emergency_access.preexpiry_notified` and `emergency_access.expired`

Configuration (`appsettings*.json`):
```json
{
  "EmergencyAccess": {
    "EnableAutoExpiryWorker": true,
    "AutoExpiryWorkerIntervalMinutes": 5,
    "PreExpiryNotificationLeadMinutes": 60
  }
}
```

### CloudFront Report CDN
Issue #58 adds CloudFront CDN infrastructure and runtime integration for report downloads:

- CloudFront signed URLs are used when `Aws:S3:CloudFront:Enabled=true`
- delete flow triggers CloudFront cache invalidation for the deleted report path
- S3 upload path writes `Cache-Control: private, no-store, max-age=0`
- infrastructure template added at `infra/aws/cloudfront-report-cdn/cloudfront-report-cdn.yaml`
  - S3 origin locked behind Origin Access Identity (OAI)
  - HTTPS-only viewer protocol
  - signed URL enforcement and no-store response headers

Configuration (`appsettings*.json` and user-secrets):
```json
{
  "Aws": {
    "S3": {
      "CloudFront": {
        "Enabled": true,
        "DistributionId": "E123ABC456XYZ",
        "DistributionDomain": "d111111abcdef8.cloudfront.net",
        "KeyPairId": "K123EXAMPLE",
        "PrivateKeyPem": "base64-encoded-cloudfront-private-key-pem",
        "EnableInvalidationOnDelete": true
      }
    }
  }
}
```

### Transport Security (TLS)
Issue #49 adds transport-level TLS enforcement controls for production paths:
- PostgreSQL connection must use `SSL Mode=Require|VerifyCA|VerifyFull`
- Redis connection must include `ssl=true`
- AWS `ServiceUrl` must use `https://` when LocalStack is disabled

Configuration:
```json
{
  "TransportSecurity": {
    "EnforceTls13": true
  }
}
```

Notes:
- `TransportSecurity:EnforceTls13=true` is enabled in `appsettings.json` (production baseline).
- `appsettings.Development.json` disables enforcement for local Docker/Aspire environments that use non-TLS local endpoints.
- In production, avoid `Trust Server Certificate=true` in PostgreSQL connection strings.

### Security Headers and CORS
Issue #50 adds strict response hardening for API responses:
- `Content-Security-Policy`
- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: no-referrer`
- HSTS configured with `preload` + `includeSubDomains`

Configuration:
```json
{
  "SecurityHeaders": {
    "ContentSecurityPolicy": "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'; object-src 'none'",
    "XFrameOptions": "DENY",
    "XContentTypeOptions": "nosniff",
    "ReferrerPolicy": "no-referrer",
    "HstsIncludeSubDomains": true,
    "HstsPreload": true,
    "HstsMaxAgeDays": 730
  }
}
```

Notes:
- CORS is allow-list based via `Cors:AllowedOrigins`; origins not listed are blocked.
- Preflight cache duration is set with `Access-Control-Max-Age` (10 minutes).

### Input Sanitization
Issue #51 introduces centralized input sanitization for user-provided text before persistence:
- HTML tags are stripped from plain text fields
- control characters are removed
- repeated whitespace is normalized
- user text persistence flows (profiles, emergency contacts, access grants, report metadata/results) use the shared sanitizer

Additional guardrails:
- JSON input is constrained to declared DTO contracts via explicit `System.Text.Json` type resolver configuration
- source guardrail tests validate that raw SQL execution patterns are not introduced in application code

### Audit Logging
Issue #52 adds a centralized audit logging service for PII/medical-data access workflows.

Captured fields:
- who: `actor_user_id`, `actor_role`
- what: `action`, `resource_type`, `resource_id`
- when: `occurredAtUtc` and `occurredAtIst` (stored in audit details)
- where: request path/method, client IP, user-agent

Notes:
- audit records are persisted in `audit_logs` through `IAuditLogRepository`
- application logs emit structured audit events via Serilog/`ILogger`; in containerized AWS deployments these logs flow to CloudWatch through runtime log drivers
- audit payloads avoid raw PII values in summary/messages

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

### Manual runtime test scenarios (Aspire + Kubernetes)
Use the two-pass runtime checklist in `testing_scenarios.md` to execute the same scenario catalog in:
- Pass A: .NET Aspire runtime
- Pass B: Kubernetes runtime

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
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.Extensions.Caching.StackExchangeRedis
- AWSSDK.Extensions.NETCore.Setup

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

### Compliance Archival (WS6-3 / Issue #53)

For DISHA-aligned long-term retention, the repository includes infrastructure artifacts to archive audit logs from CloudWatch Logs into S3 with write-once retention:

- setup script: `scripts/configure-audit-log-archival.sh`
- policy templates: `infra/aws/audit-log-archival/`
- runbook: `docs/infrastructure/audit-log-archival.md`

Implemented controls:
- CloudWatch Logs subscription -> Kinesis Data Firehose -> S3 archive
- S3 Object Lock default retention in `COMPLIANCE` mode (minimum 2190 days / 6 years)
- lifecycle transition to Glacier Deep Archive (`DEEP_ARCHIVE`) for cost optimization
- S3 versioning + public access block + default encryption (AES256 or KMS)

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
