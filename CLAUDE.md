# CLAUDE.md — Aarogya Backend

This document provides a comprehensive reference for AI assistants working in this repository.

## Project Overview

Aarogya is an ASP.NET Core 9.0 REST API for a healthcare records management platform. It follows **Clean Architecture** principles with three layers: `Aarogya.Api` (presentation), `Aarogya.Domain` (business logic), and `Aarogya.Infrastructure` (data/external services). The system handles sensitive patient data including PII-encrypted fields, Aadhaar vault records, medical reports, and access control grants.

**Company:** Kinvee Technologies
**Target region:** India (AWS `ap-south-1`)
**Auth provider:** AWS Cognito (with LocalStack for local dev)

---

## Repository Layout

```
AarogyaBackend/
├── src/
│   ├── Aarogya.Api/            # Web API layer (controllers, config, middleware)
│   ├── Aarogya.Domain/         # Entities, interfaces, specifications, enums, value objects
│   └── Aarogya.Infrastructure/ # EF Core, repositories, AWS integrations, security
├── tests/
│   ├── Aarogya.Api.Tests/      # Controller + service unit tests (xUnit, Moq)
│   ├── Aarogya.Domain.Tests/   # Domain unit tests
│   └── Aarogya.Infrastructure.Tests/ # Integration tests (Testcontainers PostgreSQL)
├── AppHost/                    # .NET Aspire orchestration host
├── docker/                     # Docker init scripts (postgres, localstack)
├── docs/                       # Architecture backlog, workstream docs
├── k8s/                        # Kubernetes manifests (namespace, api, postgres, redis, pgadmin)
├── scripts/                    # install-git-hooks.sh
├── Aarogya.sln
├── Directory.Build.props       # Solution-wide MSBuild properties (applied to all projects)
├── Directory.Packages.props    # Central NuGet package version management
├── global.json                 # .NET SDK version pin (9.0.305)
├── Dockerfile                  # Multi-stage Alpine build → aspnet:9.0-alpine runtime
└── docker-compose.yml          # Local dev stack: api, postgres, redis, localstack, pgadmin
```

---

## Build and Run Commands

### Prerequisites

- .NET 9.0 SDK (pinned to `9.0.305` via `global.json`)
- Docker Desktop
- `dotnet workload install aspire` (for AppHost)
- `kubectl` + `kind` (for Kubernetes setup)

### Build

```bash
dotnet restore
dotnet build
dotnet build --configuration Release
```

### Run Tests

```bash
# All tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./.testresults

# Specific project
dotnet test tests/Aarogya.Api.Tests/
dotnet test tests/Aarogya.Infrastructure.Tests/
```

**Note:** Infrastructure integration tests require Docker (Testcontainers spins up a real PostgreSQL 16 Alpine container, creates an isolated database per test class).

### Code Formatting

```bash
# Check formatting (excludes generated migrations)
dotnet format --verify-no-changes --verbosity diagnostic \
  --exclude src/Aarogya.Infrastructure/Persistence/Migrations

# Apply formatting fixes
dotnet format --exclude src/Aarogya.Infrastructure/Persistence/Migrations
```

### Run the API

```bash
# Local .NET only (requires Postgres + Redis + LocalStack running separately)
dotnet run --project src/Aarogya.Api

# Full stack via Docker Compose
docker compose up

# Full stack via .NET Aspire
dotnet run --project AppHost
```

**Endpoints after startup:**

| Mode | URL |
|------|-----|
| Local .NET | `http://localhost:5000` / `https://localhost:5001` |
| Docker Compose | `http://localhost:8080/swagger` |
| pgAdmin | `http://localhost:5050` |
| LocalStack | `http://localhost:4566/_localstack/health` |

### Database Migrations

```bash
# Install EF tools
dotnet tool restore

# Apply migrations (use AarogyaDbContextFactory for design-time)
dotnet ef database update \
  --project src/Aarogya.Infrastructure \
  --startup-project src/Aarogya.Infrastructure \
  --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/

# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project src/Aarogya.Infrastructure \
  --startup-project src/Aarogya.Infrastructure \
  --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/
```

Migrations live in `src/Aarogya.Infrastructure/Persistence/Migrations/`. They are treated as generated code (excluded from analyzers and formatting). Auto-migration on startup is controlled by `Database:AutoMigrateOnStartup` (true in Development/Docker, false in production).

---

## Architecture and Code Conventions

### Layer Dependencies

```
Aarogya.Api → Aarogya.Domain + Aarogya.Infrastructure
Aarogya.Infrastructure → Aarogya.Domain
Aarogya.Domain → (no project references)
```

The Domain layer is dependency-free. Infrastructure implements domain interfaces. The API composes them.

### C# Language Conventions

- **Language version:** `latest` (C# 13)
- **Nullable reference types:** enabled everywhere
- **Implicit usings:** enabled
- **Indent:** 2 spaces (enforced by `.editorconfig`)
- **Max line length:** 120 characters
- **File-scoped namespaces** are used throughout
- **`sealed`** classes are preferred for entities, records, and services that are not designed for inheritance
- **Primary constructors** are used for services and repositories (e.g., `public sealed class UserRepository(AarogyaDbContext dbContext)`)
- **Records** are used for DTOs, request/response contracts, and value objects (`sealed record`)
- **`internal`** is the default visibility for infrastructure implementations; only use `public` where required (controllers, domain interfaces, config options)

### Analyzer Rules

The project enforces `EnforceCodeStyleInBuild=true`, `EnableNETAnalyzers=true`, and includes **SonarAnalyzer.CSharp**. When a rule must be suppressed, use `[SuppressMessage]` with a `Justification` explaining why. Common suppressed rules and their justifications are documented inline throughout the codebase.

Suppressed globally (in `Directory.Build.props`):
- `CS1591` — missing XML doc comments
- `CA1506` — class coupling (controllers are excluded)
- `CA1305` — format provider
- `S6966` — Sonar async warnings

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Entities | PascalCase | `User`, `Report`, `AuditLog` |
| Interfaces | `I` prefix | `IUserRepository`, `ISpecification<T>` |
| Configuration options | `*Options` suffix | `JwtOptions`, `AwsOptions` |
| Repositories | `*Repository` suffix | `UserRepository` |
| Specifications | `*Specification` suffix | `UserByExternalAuthIdSpecification` |
| Test methods | `MethodName_Should*` | `GetCurrentUserClaims_ShouldExtractSubEmailAndRoles` |
| EF column names | `snake_case` | `email_encrypted`, `created_at` |
| Enum DB values | `snake_case` via converter | `lab_technician` |

### Commit Message Style

Semantic commits are enforced on PRs to `main` via `pr-guardrails.yml`:

```
feat(auth): implement token refresh flow
fix: correct OTP expiry window
docs: add Aadhaar vault verification script
chore: update NuGet dependencies
refactor: extract specification evaluator
perf: add covering index on report status
test: add Testcontainers PostgreSQL fixture
ci: add dependency review workflow
```

---

## Domain Model

### Entities (`src/Aarogya.Domain/Entities/`)

| Entity | Description |
|--------|-------------|
| `User` | Patient, doctor, lab technician, or admin. PII fields (name, email, phone) are AES-GCM encrypted at rest. |
| `Report` | Medical report (lab result, imaging, etc.) with S3 file reference, JSONB results/metadata, and status lifecycle. |
| `ReportParameter` | Individual named parameter values within a report. |
| `AccessGrant` | Time-scoped permission from a patient to a doctor/user to view their reports. |
| `EmergencyContact` | Patient's emergency contact; phone is encrypted. |
| `AuditLog` | Immutable record of system actions (actor, action, resource, JSONB details). |
| `AadhaarVaultRecord` | Tokenized Aadhaar number. The actual number is AES-GCM encrypted; a SHA-256 hash enables lookup. |
| `AadhaarVaultAccessLog` | Audit trail for every vault access. |

All entities implementing `IAuditableEntity` get `CreatedAt` / `UpdatedAt` automatically set by `AarogyaDbContext.ApplyAuditTimestamps()`.

### Enums (`src/Aarogya.Domain/Enums/`)

- `UserRole`: `Patient`, `Doctor`, `LabTechnician`, `Admin`
- `ReportStatus`: (defined in codebase — see `ReportStatus.cs`)
- `ReportType`: (defined in codebase — see `ReportType.cs`)
- `AccessGrantStatus`: `Active`, etc.

All enums are stored in PostgreSQL as `snake_case` strings via `EnumSnakeCaseConverter`.

### Value Objects (`src/Aarogya.Domain/ValueObjects/`)

- `AccessGrantScope` — JSONB field defining what data a grant covers
- `AuditLogDetails` — JSONB structured audit details
- `ReportMetadata` — JSONB metadata (lab, device, etc.)
- `ReportResults` — JSONB structured results
- `ReportParameterRaw` — raw parameter value type

Value objects are stored as JSONB via `JsonbValueConverter`.

---

## Repository and Specification Pattern

### Generic Repository (`IRepository<T>`)

Located at `src/Aarogya.Domain/Repositories/IRepository.cs`. All data access goes through this interface:

```csharp
GetByIdAsync(Guid id, ...)
FirstOrDefaultAsync(ISpecification<T> spec, ...)
ListAsync(ISpecification<T>? spec, ...)
CountAsync(ISpecification<T>? spec, ...)
AddAsync(T entity, ...)
AddRangeAsync(IEnumerable<T> entities, ...)
Update(T entity)
Delete(T entity)
```

### Specifications (`src/Aarogya.Domain/Specifications/`)

Extend `BaseSpecification<T>` to encapsulate query logic. Available builders:

```csharp
protected void AddInclude(Expression<Func<T, object>> expr)
protected void ApplyOrderBy(Expression<Func<T, object>> expr)
protected void ApplyOrderByDescending(Expression<Func<T, object>> expr)
protected void ApplyPaging(int skip, int take)
protected void ApplyAsNoTracking()
```

Example existing specifications: `UserByExternalAuthIdSpecification`, `ReportsByPatientSpecification`, `ActiveAccessGrantSpecification`.

### Unit of Work (`IUnitOfWork`)

```csharp
SaveChangesAsync(CancellationToken)
BeginTransactionAsync(CancellationToken)
CommitTransactionAsync(CancellationToken)
RollbackTransactionAsync(CancellationToken)
```

Always use `IUnitOfWork.SaveChangesAsync()` to commit. Do not call `DbContext.SaveChangesAsync()` directly from application code.

---

## Security Architecture

### PII Field Encryption

Sensitive text fields (names, emails, phone numbers) are encrypted with **AES-256-GCM** before storage. The payload format is:

```
[ 1 byte version | 12 bytes nonce | 16 bytes auth tag | N bytes ciphertext ]
```

`IPiiFieldEncryptionService` (`PiiFieldEncryptionService`) handles encrypt/decrypt. EF Core value converters (`EncryptedRequiredStringToBytesConverter`, `EncryptedNullableStringToBytesConverter`) integrate this transparently into entity configurations.

**Configuration (`Encryption` section):**
- `UseAwsKms: true` → data key generated via AWS KMS (`GenerateDataKey`)
- `UseAwsKms: false` → data key derived from `LocalDataKey` secret (dev/test only)

### Blind Indexes

Because encrypted fields cannot be queried directly, `BlindIndexService` computes **HMAC-SHA256** hashes over normalized field values (e.g., `TRIM().ToUpperInvariant()`). These are stored alongside encrypted columns (`email_hash`, `phone_hash`) and indexed in PostgreSQL. Blind indexes are automatically computed in `AarogyaDbContext.ApplyBlindIndexes()` during `SaveChanges`.

Scoping format: `"{scope}:{normalized_value}"` (e.g., `"users.email:USER@EXAMPLE.COM"`).

### Aadhaar Data Vault

`IAadhaarVaultService` / `AadhaarVaultService` implements a tokenization vault:
- Aadhaar numbers are normalized and SHA-256 hashed for lookup
- The actual number is AES-GCM encrypted and stored in `aadhaar_vault_records`
- A UUID reference token is returned to the caller
- Every access is logged in `aadhaar_vault_access_logs`
- The mock Aadhaar API (`IMockAadhaarApiClient`) is used in development

### Authentication

Dual-scheme JWT authentication is configured in `AuthenticationExtensions`:

1. **CognitoJwt** (default): Validates AWS Cognito JWTs. Issuer is auto-resolved from `Aws:Cognito:UserPoolId` and `Aws:Region`, or overridden via `Aws:Cognito:Issuer`. `RequireHttpsMetadata` is false when LocalStack is in use.
2. **LocalJwt** (optional): For local dev/testing without Cognito. Active only when `Jwt:Key` is configured with a valid 32+ character key. The token issuer is checked at runtime to select the right scheme.

Claim mappings: `sub` → `NameClaimType`, `cognito:groups` → `RoleClaimType`.

---

## Configuration System

### Configuration Sources (priority order)

1. `appsettings.json` — base defaults (mostly sentinel placeholders)
2. `appsettings.{Environment}.json` — environment overrides
3. User secrets (`dotnet user-secrets`) — for local dev secrets
4. Environment variables prefixed with `AAROGYA_` — production/CI

Environment variable mapping: `AAROGYA_Aws__Cognito__UserPoolId` → `Aws:Cognito:UserPoolId`.

### Key Configuration Sections

| Section | Class | Description |
|---------|-------|-------------|
| `Aws` | `AwsOptions` | Region, LocalStack, Cognito, S3, SES settings |
| `Jwt` | `JwtOptions` | Local JWT key/issuer/audience (dev only) |
| `ConnectionStrings:DefaultConnection` | — | PostgreSQL Npgsql connection string |
| `ConnectionStrings:Redis` | — | StackExchange.Redis connection string |
| `Redis` | `RedisOptions` | Instance name, DB index, timeouts |
| `Cors` | `CorsOptions` | `AllowedOrigins`, `AllowCredentials` |
| `Database` | `DatabaseOptions` | EF Core timeouts, retry, auto-migrate |
| `Encryption` | `EncryptionOptions` | KMS mode, local key, blind index key |
| `Otp` | `OtpOptions` | OTP code length, expiry, rate limit |
| `Pkce` | `PkceOptions` | Authorization code and token expiry |
| `SeedData` | `SeedDataOptions` | Seed user/report counts, enable flag |
| `AadhaarVault` | `AadhaarVaultOptions` | Mock API base URL and endpoints |

### Startup Validation

`StartupExtensions.ValidateRequiredConfiguration()` checks for:
- `ConnectionStrings:DefaultConnection` present
- `Aws:Cognito:UserPoolId` and `AppClientId` not placeholders
- No default dev credentials in non-Development environments
- Valid CORS origin URLs

In `Development`, violations are warnings. In other environments, they throw and abort startup.

---

## API Layer

### Controllers (`src/Aarogya.Api/Controllers/`)

Currently one controller: `AuthController` at route `api/auth`.

**Endpoints:**

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/auth/otp/request` | Anonymous | Request a phone OTP |
| `POST` | `/api/auth/otp/verify` | Anonymous | Verify a phone OTP |
| `POST` | `/api/auth/pkce/authorize` | Anonymous | Create PKCE authorization code |
| `POST` | `/api/auth/pkce/token` | Anonymous | Exchange PKCE code for tokens |
| `POST` | `/api/auth/token/refresh` | Anonymous | Refresh access token |
| `POST` | `/api/auth/token/revoke` | Anonymous | Revoke refresh token |
| `GET`  | `/api/auth/me` | Bearer JWT | Return claims from current JWT |

Controllers must be `public sealed` and annotated with `[ApiController]`. Response types are declared via `[ProducesResponseType]`. All request models use `[Required]` data annotations. New controllers follow the same pattern: group related endpoints under a single route prefix.

### Request/Response DTOs

DTOs live in the same file as their controller (for small controllers) or in a `Contracts/` folder. They are `sealed record` types. `[Required]` is applied via property-level attribute syntax:

```csharp
public sealed record OtpRequestCommand(
  [property: System.ComponentModel.DataAnnotations.Required]
  string PhoneNumber);
```

The `CA1515` analyzer rule (make public types internal) is suppressed on controller-bound types with a standard justification comment.

### Middleware and Pipeline Order

1. `UseAarogyaRequestLogging` (Serilog structured request logging)
2. `UseHttpsRedirection`
3. `UseCors("AarogyaPolicy")`
4. `UseAuthentication`
5. `UseAuthorization`
6. `MapControllers`
7. Health check endpoints: `/health` (all checks), `/health/ready` (tagged `ready`)

### Health Checks

- **PostgreSQL:** `PostgreSqlConnectionHealthCheck` — tagged `db`, `ready`
- **Redis:** `RedisDistributedCacheHealthCheck` — tagged `cache`, `ready` (registered only when Redis connection string is present)

---

## Infrastructure Layer

### Persistence (`src/Aarogya.Infrastructure/Persistence/`)

- **`AarogyaDbContext`**: Registers all `DbSet<T>` properties and applies `IEntityTypeConfiguration<T>` via `ApplyConfiguration`. Overrides `SaveChanges`/`SaveChangesAsync` to auto-set audit timestamps and compute blind indexes.
- **`AarogyaDbContextFactory`**: Design-time factory for EF Core migrations.
- **Configurations (`Configurations/`)**: Each entity has its own `IEntityTypeConfiguration<T>` class. Table names use `snake_case`. All column names are explicitly set. PII columns map to `bytea` with value converters.
- **Converters (`Converters/`)**: `EncryptedStringToBytesConverter`, `EnumSnakeCaseConverter`, `JsonbValueConverter`.
- **Repositories (`Repositories/`)**: Internal implementations of domain repository interfaces. `Repository<T>` is the generic base; entity-specific repositories (e.g., `UserRepository`) extend it for custom queries.

### AWS Services (`src/Aarogya.Infrastructure/Aws/`)

`AwsServiceRegistration.AddAwsServices()` registers:
- `IAmazonS3` — file storage
- `IAmazonSimpleEmailServiceV2` — transactional email
- `IAmazonKeyManagementService` — envelope encryption data key generation

When `Aws:UseLocalStack=true`, the `ServiceURL` is pointed at the LocalStack endpoint.

### Caching

Redis distributed cache is registered when `ConnectionStrings:Redis` is present. The `RedisDistributedCacheHealthCheck` wraps `IDistributedCache` for health endpoint integration. Instance name prefix defaults to `aarogya_`.

### Seeding (`src/Aarogya.Infrastructure/Seeding/`)

`DevelopmentDataSeeder` (implements `IDataSeeder`) runs at startup when `SeedData:EnableOnStartup=true`. It checks for existing seed data (via `ExternalAuthId` prefix `seed-`) before inserting. Uses the **Bogus** library for fake data generation. The seeder creates users (patients, doctors, lab techs, admins), emergency contacts, reports, and Aadhaar vault records.

---

## Testing

### Test Projects

| Project | Framework | Dependencies |
|---------|-----------|-------------|
| `Aarogya.Api.Tests` | xUnit, Moq, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` | References `Aarogya.Api` |
| `Aarogya.Domain.Tests` | xUnit, Moq, FluentAssertions | References `Aarogya.Domain` |
| `Aarogya.Infrastructure.Tests` | xUnit, Moq, FluentAssertions, Testcontainers.PostgreSql | References `Aarogya.Infrastructure` |

### Integration Test Setup

`PostgreSqlContainerFixture` (shared via `[Collection]`) starts a single PostgreSQL 16 Alpine container for the test run. Each test class gets an isolated database (created via `CREATE DATABASE` with a random GUID name). EF Core migrations are applied via `dbContext.Database.MigrateAsync()` per isolated database.

```csharp
[Collection(PostgreSqlIntegrationFixtureGroup.CollectionName)]
public sealed class MyIntegrationTests(PostgreSqlContainerFixture fixture)
{
  [Fact]
  public async Task SomeTest()
  {
    await using var serviceProvider = await fixture.CreateServiceProviderAsync();
    // test with real PostgreSQL
  }
}
```

### Test Naming

```csharp
// Pattern: MethodOrScenario_ShouldExpectedBehavior
public async Task SaveChanges_ShouldPersistEncryptedEntityAsync()
public void GetCurrentUserClaims_ShouldExtractSubEmailAndRoles()
```

### Analyzer Rules in Tests

The `Directory.Build.props` suppresses additional rules for test projects:
- `CA1707` — allow underscores in test method names
- `CA2007` — don't require `ConfigureAwait` in test code
- `CA1515` — allow public test classes

---

## Development Workflow

### Git Hooks

Install secret-scanning pre-commit hook (requires `ripgrep`):

```bash
bash scripts/install-git-hooks.sh
```

The hook scans staged files for AWS access key patterns, private keys, and JWT/credential patterns. Allowed exceptions: `secrets.template.json`, `.env.example`, `README.md`.

### Adding a New Feature (typical flow)

1. **Domain:** Add entity to `Aarogya.Domain/Entities/`, add enums/value objects as needed, add repository interface to `Aarogya.Domain/Repositories/`, add specifications to `Aarogya.Domain/Specifications/`.
2. **Infrastructure:** Add EF Core entity configuration in `Configurations/`, implement repository in `Repositories/`, register in `DependencyInjection.cs`, create and apply migration.
3. **API:** Add controller with `[ApiController]`, define request/response records, inject services via constructor.
4. **Tests:** Add unit tests in `Aarogya.Api.Tests` or `Aarogya.Domain.Tests`; add integration tests in `Aarogya.Infrastructure.Tests` using the PostgreSQL fixture.

### Adding a New Entity

1. Create entity class in `src/Aarogya.Domain/Entities/` — implement `IAuditableEntity` if it needs `CreatedAt`/`UpdatedAt`.
2. Create `IEntityTypeConfiguration<T>` in `src/Aarogya.Infrastructure/Persistence/Configurations/`.
3. Add `DbSet<T>` property and `modelBuilder.ApplyConfiguration(...)` call in `AarogyaDbContext`.
4. Add repository interface in `src/Aarogya.Domain/Repositories/`.
5. Add repository implementation in `src/Aarogya.Infrastructure/Persistence/Repositories/` (extend `Repository<T>`).
6. Register the repository (`AddScoped<IRepo, RepoImpl>()`) in `DependencyInjection.cs`.
7. Create and apply EF Core migration.

### Adding a New Configuration Section

1. Create `*Options` class in `src/Aarogya.Api/Configuration/` with a `SectionName` constant.
2. Register in `Program.cs` using `AddOptionsWithValidateOnStart<T>().BindConfiguration(T.SectionName).ValidateDataAnnotations()`.
3. Add default (sentinel) values to `appsettings.json` and working dev values to `appsettings.Development.json`.
4. Document the environment variable mapping (e.g., `AAROGYA_Section__Key`).

---

## CI/CD

### GitHub Actions Workflows

**`dotnet-ci.yml`** — runs on push to `main`/`develop` and on all PRs:
- `build-and-test`: restore → build (Release) → test with coverage → publish artifact
- `lint`: `dotnet format --verify-no-changes` (excludes Migrations)

**`pr-guardrails.yml`** — runs on PRs to `main`:
- Validates PR title follows Conventional Commits (feat, fix, docs, chore, refactor, perf, test, ci)
- Optional dependency review (`ENABLE_DEPENDENCY_REVIEW=true` repo variable)

### Artifacts

- Build output: `artifacts/bin/<ProjectName>/` (configured via `Directory.Build.props`)
- Intermediate files: `artifacts/obj/<ProjectName>/`
- Test results: `./.testresults/*.trx`
- Published API: `./publish/`

---

## Docker and Deployment

### Docker Compose Services

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| `api` | `aarogya-api:dev` (local build) | `8080` | ASP.NET Core API |
| `postgres` | `postgres:16` | `5432` | Primary database |
| `postgres-init` | `postgres:16` | — | Installs `pgcrypto` extension |
| `redis` | `redis:7` | `6379` | Distributed cache |
| `localstack` | `localstack/localstack:3` | `4566` | AWS services (S3, Cognito, KMS, SES) |
| `pgadmin` | `dpage/pgadmin4:8` | `5050` | Database admin UI |

All services share the `aarogya-net` bridge network. The API container waits for health checks on postgres, redis, and localstack before starting.

LocalStack services enabled: `s3,sqs,cognito,kms,ses`.

### Dockerfile

Multi-stage build:
1. `build` stage: `dotnet/sdk:9.0-alpine` — restore, publish
2. `final` stage: `dotnet/aspnet:9.0-alpine` — runs as non-root user (`$APP_UID`), listens on port `8080`

### Kubernetes (kind)

Manifests in `k8s/`: namespace `aarogya`, deployments for api, postgres, redis, pgadmin. Access via `kubectl port-forward`.

---

## Known Patterns and Anti-Patterns to Follow

**Do:**
- Use `sealed` for all concrete classes that aren't designed for inheritance
- Use primary constructors for dependency injection
- Use `ISpecification<T>` for all queries — never write raw LINQ in controllers or services
- Use `IUnitOfWork.SaveChangesAsync()` to persist changes
- Use `CancellationToken` in all async methods and pass it through
- Apply `[SuppressMessage]` with a `Justification` when suppressing analyzers
- Use `DateTimeOffset` (not `DateTime`) for all timestamps — the system is UTC-aware
- Store all enum values as snake_case strings in the database
- Use `AAROGYA_` prefixed environment variables for all external configuration

**Don't:**
- Add direct `DbContext` dependencies in controllers or domain services
- Write LINQ queries outside of `Repository<T>` or `ISpecification<T>` implementations
- Put secrets or real credentials in `appsettings.json` — use user-secrets or environment variables
- Reference `Aarogya.Infrastructure` from `Aarogya.Domain`
- Use `DateTime.Now` or `DateTime.UtcNow` — inject `IUtcClock` where a clock is needed
- Skip the `pgcrypto` extension — it is required for UUID generation server-side
- Mark migration files as non-generated — they are excluded from formatting and analysis by `.editorconfig`
