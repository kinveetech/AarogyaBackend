# Workstream 1: Dev Environment & Project Setup

## Overview
Setting up the local development environment and project scaffolding for the Aarogya healthcare backend.

**Tech Stack:** ASP.NET Core 8 (C#), PostgreSQL, AWS (Cognito, S3, SQS, KMS), Redis, .NET Aspire, LocalStack, Dev Containers

## Tasks

### 1. Initialize .NET Solution Structure
**Description:** Create Aarogya.sln with projects: Aarogya.Api, Aarogya.Domain, Aarogya.Infrastructure, Aarogya.Tests. Use .NET 8 SDK.

**Acceptance Criteria:**
- Solution file exists with all four projects
- Projects follow clean architecture structure
- `dotnet build` succeeds without errors

**Complexity:** S | **Dependencies:** None

---

### 2. Configure .NET Aspire App Host
**Description:** Create Aarogya.AppHost project to orchestrate PostgreSQL, Redis, LocalStack, and the API.

**Acceptance Criteria:**
- AppHost configuration includes PostgreSQL, Redis, LocalStack, and API services
- `dotnet run --project AppHost` starts all services
- Aspire dashboard accessible

**Complexity:** M | **Dependencies:** 1

---

### 3. Set Up Dev Containers
**Description:** Create .devcontainer/devcontainer.json and docker-compose.yml with .NET 8 SDK, AWS CLI, PostgreSQL client.

**Acceptance Criteria:**
- VS Code "Reopen in Container" works
- dotnet CLI, AWS CLI, psql available inside container
- C# Dev Kit extensions installed

**Complexity:** M | **Dependencies:** 1

---

### 4. Configure LocalStack for AWS Services
**Description:** Set up LocalStack container with S3, SQS, Cognito, KMS. Create initialization scripts.

**Acceptance Criteria:**
- Initialization scripts create default S3 buckets, SQS queues, Cognito user pool
- AWS CLI commands against LocalStack succeed
- Health endpoint responds

**Complexity:** M | **Dependencies:** 2

---

### 5. Set Up EditorConfig & Code Style
**Description:** Configure .editorconfig, Directory.Build.props for consistent formatting.

**Acceptance Criteria:**
- .editorconfig with C# style rules
- `dotnet format` produces no changes on clean code
- Nullable reference types enabled

**Complexity:** S | **Dependencies:** 1

---

### 6. Configure Solution-Level NuGet Packages
**Description:** Set up Directory.Packages.props for central package management. Add core packages (EF Core, AWS SDK, Serilog, FluentValidation).

**Acceptance Criteria:**
- Central Package Management enabled
- No version numbers in individual project files
- `dotnet restore` succeeds

**Complexity:** S | **Dependencies:** 1

---

### 7. Create Docker Compose for Local Services
**Description:** PostgreSQL 16 with pgcrypto, Redis 7, pgAdmin.

**Acceptance Criteria:**
- `docker compose up` starts all services
- pgAdmin accessible, PostgreSQL and Redis on standard ports
- Data persistence via volumes

**Complexity:** S | **Dependencies:** None

---

### 8. Set Up Environment Configuration
**Description:** Create appsettings.Development.json, user-secrets for local dev, environment variable mapping.

**Acceptance Criteria:**
- Configuration sections for DB, AWS, Redis, logging
- User secrets initialized for sensitive data
- No sensitive data committed to source control

**Complexity:** S | **Dependencies:** 1

---

## Assumptions
- Docker Desktop installed
- .NET 8 SDK installed locally
- VS Code with C# Dev Kit is the primary IDE
- LocalStack free tier sufficient for development

## Risks
| Risk | Impact | Mitigation |
|------|--------|------------|
| LocalStack may not fully emulate Cognito | Medium | Test against real Cognito early |
| .NET Aspire is relatively new | Medium | Maintain Docker Compose fallback |
