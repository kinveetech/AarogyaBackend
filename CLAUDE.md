# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Aarogya is an ASP.NET Core 9.0 REST API for a healthcare records management platform (Clean Architecture). Handles sensitive patient data with PII encryption, Aadhaar vault tokenization, medical reports, and access control grants.

**Company:** Kinvee Technologies | **Region:** India (AWS `ap-south-1`) | **Auth:** AWS Cognito (LocalStack for local dev)

## Build and Run Commands

```bash
dotnet restore && dotnet build                # Build
dotnet build --configuration Release          # Release build
dotnet test                                   # All tests
dotnet test tests/Aarogya.Api.Tests/          # Specific test project
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Single test
dotnet test --collect:"XPlat Code Coverage" --results-directory ./.testresults  # With coverage

# Formatting (CI runs this — excludes generated migrations)
dotnet format --verify-no-changes --verbosity diagnostic \
  --exclude src/Aarogya.Infrastructure/Persistence/Migrations
dotnet format --exclude src/Aarogya.Infrastructure/Persistence/Migrations  # Auto-fix

# Run
dotnet run --project src/Aarogya.Api          # Local (needs Postgres+Redis+LocalStack)
docker compose up                             # Full stack
dotnet run --project AppHost                  # .NET Aspire

# Database migrations
dotnet tool restore
dotnet ef migrations add <Name> \
  --project src/Aarogya.Infrastructure \
  --startup-project src/Aarogya.Infrastructure \
  --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/
dotnet ef database update \
  --project src/Aarogya.Infrastructure \
  --startup-project src/Aarogya.Infrastructure \
  --msbuildprojectextensionspath artifacts/obj/Aarogya.Infrastructure/
```

**Infrastructure tests require Docker** — Testcontainers spins up PostgreSQL 16 Alpine per test class.

## Architecture

```
Aarogya.Api → Aarogya.Domain + Aarogya.Infrastructure
Aarogya.Infrastructure → Aarogya.Domain
Aarogya.Domain → (no project references — dependency-free)
```

### Source Layout

```
src/Aarogya.Api/
├── Controllers/              # AuthController (api/auth)
│   └── V1/                   # Versioned controllers (api/v1/*)
│       ├── UsersController, ReportsController, AccessGrantsController
│       ├── EmergencyContactsController, EmergencyAccessController
│       ├── ConsentsController, NotificationsController
├── Features/V1/              # Service interfaces, implementations, contracts per feature
│   ├── Users/                # IUserProfileService, IUserDataRightsService, Cached*
│   ├── Reports/              # IReportService, virus scanning, S3 upload, CDN invalidation
│   ├── AccessGrants/         # IAccessGrantService, CachedAccessGrantService
│   ├── EmergencyContacts/    # IEmergencyContactService
│   ├── EmergencyAccess/      # IEmergencyAccessService, audit trail, expiry worker
│   ├── Consents/             # IConsentService, ConsentPurposeCatalog
│   └── Notifications/        # Push, Email, SMS senders + preference service
├── Authentication/           # OTP, PKCE, social auth, API key handler
├── Authorization/            # Role policies, claims transformation
├── Validation/               # FluentValidation validators
├── Configuration/            # *Options classes with SectionName constants
└── Program.cs

src/Aarogya.Domain/
├── Entities/                 # User, Report, ReportParameter, AccessGrant,
│                             # EmergencyContact, ConsentRecord, AuditLog,
│                             # AadhaarVaultRecord, AadhaarVaultAccessLog
├── Enums/                    # UserRole, ReportStatus, ReportType, AccessGrantStatus
├── ValueObjects/             # AccessGrantScope, ReportMetadata, ReportResults, etc.
├── Repositories/             # IRepository<T>, IUserRepository, IUnitOfWork, etc.
└── Specifications/           # BaseSpecification<T> + per-entity specs

src/Aarogya.Infrastructure/
├── Persistence/
│   ├── AarogyaDbContext.cs   # DbSets, audit timestamps, blind index computation
│   ├── Configurations/       # IEntityTypeConfiguration<T> per entity (snake_case tables)
│   ├── Converters/           # Encrypted string, enum snake_case, JSONB converters
│   ├── Repositories/         # Repository<T>, entity-specific repos, UnitOfWork
│   └── Migrations/           # Generated — excluded from formatting/analyzers
├── Aws/                      # S3, SES, KMS service registration
├── Security/                 # PiiFieldEncryptionService, BlindIndexService, AadhaarVaultService
└── Seeding/                  # DevelopmentDataSeeder (Bogus faker)
```

### Key Patterns

- **Repository + Specification**: All queries use `ISpecification<T>` extending `BaseSpecification<T>`. Never write raw LINQ in controllers/services.
- **Unit of Work**: Always use `IUnitOfWork.SaveChangesAsync()` — never `DbContext.SaveChangesAsync()` directly.
- **PII Encryption**: Sensitive fields encrypted via AES-256-GCM with EF Core value converters. Blind indexes (HMAC-SHA256) enable querying encrypted columns.
- **Features Pattern**: Each feature area in `Features/V1/` has an interface (`I*Service`), implementation, contracts file (`*Contracts.cs`), and optionally a cached decorator (`Cached*Service`).
- **Audit Timestamps**: Entities implementing `IAuditableEntity` get `CreatedAt`/`UpdatedAt` auto-set by `AarogyaDbContext`.

## Code Conventions

- **C# 13**, nullable reference types enabled, implicit usings enabled
- **2-space indent** (enforced by `.editorconfig`), **120 char max line length**
- **File-scoped namespaces** throughout
- **`sealed`** on all concrete classes not designed for inheritance
- **Primary constructors** for DI in services/repositories
- **`sealed record`** for DTOs, request/response contracts, value objects
- **`internal`** default visibility for infrastructure; `public` only where required (controllers, domain interfaces, config options)
- **`DateTimeOffset`** for all timestamps — inject `IUtcClock` where a clock is needed (never `DateTime.Now`/`DateTime.UtcNow`)
- Enums stored as **snake_case strings** in PostgreSQL via `EnumSnakeCaseConverter`
- PII columns map to **`bytea`** with encryption value converters
- JSONB columns use `JsonbValueConverter`

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Entities | PascalCase | `User`, `Report`, `AuditLog` |
| Interfaces | `I` prefix | `IUserRepository`, `ISpecification<T>` |
| Config options | `*Options` + `SectionName` const | `JwtOptions`, `AwsOptions` |
| Repositories | `*Repository` suffix | `UserRepository` |
| Specifications | `*Specification` suffix | `UserByExternalAuthIdSpecification` |
| Test methods | `MethodName_Should*` | `SaveChanges_ShouldPersistEncryptedEntityAsync` |
| DB columns | `snake_case` | `email_encrypted`, `created_at` |

### Analyzer Enforcement

`EnforceCodeStyleInBuild=true`, `EnableNETAnalyzers=true`, SonarAnalyzer.CSharp. Suppress with `[SuppressMessage]` + `Justification`. Globally suppressed in `Directory.Build.props`: `CS1591`, `CA1506`, `CA1305`, `S6966`. Test projects additionally suppress `CA1707`, `CA2007`, `CA1515`.

### Commit Messages

Conventional Commits enforced on PRs to `main` via `pr-guardrails.yml`:
```
feat(auth): implement token refresh flow
fix: correct OTP expiry window
refactor: extract specification evaluator
```

## Testing

| Project | Type | Notes |
|---------|------|-------|
| `Aarogya.Api.Tests` | Unit (xUnit, Moq, FluentAssertions) | Controller + service tests |
| `Aarogya.Domain.Tests` | Unit | Domain logic tests |
| `Aarogya.Infrastructure.Tests` | Integration (Testcontainers) | Real PostgreSQL 16; isolated DB per test class |

Integration tests use `PostgreSqlContainerFixture` shared via `[Collection]`:
```csharp
[Collection(PostgreSqlIntegrationFixtureGroup.CollectionName)]
public sealed class MyTests(PostgreSqlContainerFixture fixture)
{
  [Fact]
  public async Task SomeTest()
  {
    await using var sp = await fixture.CreateServiceProviderAsync();
    // test with real PostgreSQL
  }
}
```

## Adding New Features (typical flow)

1. **Domain**: Entity in `Entities/`, enums/value objects as needed, repository interface in `Repositories/`, specifications in `Specifications/`
2. **Infrastructure**: `IEntityTypeConfiguration<T>` in `Configurations/`, `DbSet<T>` + `ApplyConfiguration` in `AarogyaDbContext`, repository impl in `Repositories/` extending `Repository<T>`, register `AddScoped<IRepo, Impl>()` in `DependencyInjection.cs`, create migration
3. **API**: Controller (`public sealed`, `[ApiController]`), contracts (`sealed record`), service interface + implementation in `Features/V1/`, register in `V1FeatureServiceCollectionExtensions`
4. **Tests**: Unit tests in `Api.Tests`/`Domain.Tests`, integration tests in `Infrastructure.Tests`

## Configuration

Sources (priority): `appsettings.json` → `appsettings.{Env}.json` → user-secrets → `AAROGYA_`-prefixed env vars.

New config sections: create `*Options` in `Configuration/` with `SectionName` constant, register via `AddOptionsWithValidateOnStart<T>().BindConfiguration(T.SectionName).ValidateDataAnnotations()` in `Program.cs`.

Startup validation in `StartupExtensions.ValidateRequiredConfiguration()` checks required keys and rejects placeholder values in non-Development environments.

## CI/CD

- **`dotnet-ci.yml`**: build (Release) → test with coverage → lint (`dotnet format --verify-no-changes`)
- **`pr-guardrails.yml`**: Conventional Commit title validation on PRs to `main`
- Build artifacts: `artifacts/bin/<Project>/`, test results: `.testresults/`

## Do / Don't

**Do:**
- `sealed` all concrete classes; primary constructors for DI
- `ISpecification<T>` for all queries; `IUnitOfWork.SaveChangesAsync()` to persist
- `CancellationToken` in all async methods, passed through
- `[SuppressMessage]` with `Justification` when suppressing analyzers
- `DateTimeOffset` for timestamps; `IUtcClock` for current time
- Enums as snake_case strings in DB

**Don't:**
- `DbContext` in controllers or domain services
- LINQ outside `Repository<T>` or `ISpecification<T>`
- Secrets in `appsettings.json` — use user-secrets or env vars
- Reference `Aarogya.Infrastructure` from `Aarogya.Domain`
- `DateTime.Now` / `DateTime.UtcNow` — use `IUtcClock`
- Migration files excluded from formatting/analysis — don't change that
