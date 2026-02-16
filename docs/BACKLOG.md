# Aarogya Backend — Master Implementation Backlog

> Auto-generated from architecture plan. 9 workstreams, 88 tasks total.
> Cross-workstream dependencies are noted as `WS{n}-{task}` (e.g., WS1-4 = Workstream 1, Task 4).

---

## Complexity Summary

| Complexity | Count | Description |
|------------|-------|-------------|
| **S** (Small) | 16 | < 1 day |
| **M** (Medium) | 38 | 1-3 days |
| **L** (Large) | 27 | 3-5 days |
| **XL** (Extra Large) | 7 | 5-10+ days |
| **Total** | **88** | |

---

## Phase 1: Foundation (Weeks 1-3)

> Goal: Local dev environment running, solution compiles, CI pipeline active.

| # | Task | Workstream | Complexity | Dependencies | Status |
|---|------|-----------|------------|-------------|--------|
| 1 | Initialize .NET Solution Structure | [WS1](workstreams/01-dev-environment.md) | S | None | |
| 2 | Create Docker Compose for Local Services | [WS1](workstreams/01-dev-environment.md) | S | None | |
| 3 | Set Up EditorConfig & Code Style | [WS1](workstreams/01-dev-environment.md) | S | WS1-1 | |
| 4 | Configure Solution-Level NuGet Packages | [WS1](workstreams/01-dev-environment.md) | S | WS1-1 | |
| 5 | Set Up Environment Configuration | [WS1](workstreams/01-dev-environment.md) | S | WS1-1 | |
| 6 | Configure .NET Aspire App Host | [WS1](workstreams/01-dev-environment.md) | M | WS1-1 | |
| 7 | Set Up Dev Containers | [WS1](workstreams/01-dev-environment.md) | M | WS1-1 | |
| 8 | Configure LocalStack for AWS Services | [WS1](workstreams/01-dev-environment.md) | M | WS1-2 | |
| 9 | Create GitHub Actions CI Pipeline | [WS8](workstreams/08-infrastructure-cicd.md) | M | WS1-1 | |
| 10 | Create Dockerfile for API | [WS8](workstreams/08-infrastructure-cicd.md) | M | WS1-1 | |

**Phase 1 Exit Criteria:**
- `dotnet build` succeeds
- `docker compose up` starts PostgreSQL, Redis, pgAdmin
- LocalStack running with S3, SQS, Cognito, KMS
- GitHub Actions CI runs on every PR

---

## Phase 2: Data Layer (Weeks 3-6)

> Goal: Database schema designed, EF Core configured, PII encryption working, Aadhaar vault implemented.

| # | Task | Workstream | Complexity | Dependencies | Status |
|---|------|-----------|------------|-------------|--------|
| 11 | Design Core Database Schema | [WS2](workstreams/02-database-data-layer.md) | L | None | |
| 12 | Configure EF Core with PostgreSQL | [WS2](workstreams/02-database-data-layer.md) | M | WS1-1 | |
| 13 | Create EF Core Entity Models | [WS2](workstreams/02-database-data-layer.md) | L | WS2-1, WS2-2 | |
| 14 | Implement Initial Migration | [WS2](workstreams/02-database-data-layer.md) | M | WS2-3 | |
| 15 | Set Up Database Health Checks | [WS2](workstreams/02-database-data-layer.md) | S | WS2-2 | |
| 16 | Implement Repository Pattern | [WS2](workstreams/02-database-data-layer.md) | L | WS2-3 | |
| 17 | Configure JSONB Indexing | [WS2](workstreams/02-database-data-layer.md) | M | WS2-4 | |
| 18 | Implement Field-Level PII Encryption | [WS2](workstreams/02-database-data-layer.md) | XL | WS2-3, WS1-4 | |
| 19 | Design Aadhaar Data Vault | [WS2](workstreams/02-database-data-layer.md) | XL | WS2-5 | |
| 20 | Create Seed Data for Development | [WS2](workstreams/02-database-data-layer.md) | M | WS2-4 | |
| 21 | Set Up xUnit Test Infrastructure | [WS9](workstreams/09-advanced-features.md) | M | WS1-1, WS2-2 | |

**Phase 2 Exit Criteria:**
- All tables created via EF Core migrations
- PII columns encrypted/decrypted transparently
- Aadhaar Data Vault operational (hash lookup + encrypted storage)
- Seed data populates dev environment
- Test infrastructure with Testcontainers running

---

## Phase 3: Authentication & Core API (Weeks 6-10)

> Goal: Users can sign up, authenticate, upload/view reports, manage access grants.

| # | Task | Workstream | Complexity | Dependencies | Status |
|---|------|-----------|------------|-------------|--------|
| 22 | Configure AWS Cognito User Pool | [WS3](workstreams/03-authentication-authorization.md) | M | WS1-4 | |
| 23 | Implement JWT Validation Middleware | [WS3](workstreams/03-authentication-authorization.md) | M | WS3-1 | |
| 24 | Implement Phone OTP Verification | [WS3](workstreams/03-authentication-authorization.md) | M | WS3-1 | |
| 25 | Implement OAuth2 PKCE Flow | [WS3](workstreams/03-authentication-authorization.md) | M | WS3-1 | |
| 26 | Implement Token Refresh Flow | [WS3](workstreams/03-authentication-authorization.md) | M | WS3-3 | |
| 27 | Implement RBAC | [WS3](workstreams/03-authentication-authorization.md) | L | WS3-3 | |
| 28 | Integrate Social Identity Providers | [WS3](workstreams/03-authentication-authorization.md) | L | WS3-1 | |
| 29 | Implement API Key Auth (Lab Systems) | [WS3](workstreams/03-authentication-authorization.md) | M | WS3-3 | |
| 30 | Set Up Minimal API Structure | [WS4](workstreams/04-core-api.md) | M | WS1-1 | |
| 31 | Configure Swagger/OpenAPI | [WS4](workstreams/04-core-api.md) | S | WS4-1 | |
| 32 | Implement Request Validation (FluentValidation) | [WS4](workstreams/04-core-api.md) | M | WS4-1 | |
| 33 | Implement Rate Limiting | [WS4](workstreams/04-core-api.md) | M | WS4-1 | |
| 34 | Implement Health Check Endpoints | [WS4](workstreams/04-core-api.md) | S | WS2-9 | |
| 35 | Configure S3 Bucket for Reports | [WS5](workstreams/05-file-storage-cdn.md) | M | WS1-4 | |
| 36 | Implement Signed URL Generation | [WS5](workstreams/05-file-storage-cdn.md) | M | WS5-1 | |
| 37 | Implement File Upload Service | [WS5](workstreams/05-file-storage-cdn.md) | M | WS5-1 | |
| 38 | Implement File Checksum Verification | [WS5](workstreams/05-file-storage-cdn.md) | S | WS5-3 | |
| 39 | Configure S3 Event Notifications | [WS5](workstreams/05-file-storage-cdn.md) | S | WS5-1, WS1-4 | |
| 40 | Implement User Profile Endpoints | [WS4](workstreams/04-core-api.md) | M | WS2-7, WS3-3 | |
| 41 | Implement Report Upload Endpoint | [WS4](workstreams/04-core-api.md) | L | WS2-3, WS5-1 | |
| 42 | Implement Report Listing Endpoint | [WS4](workstreams/04-core-api.md) | L | WS2-8, WS3-6 | |
| 43 | Implement Report Detail Endpoint | [WS4](workstreams/04-core-api.md) | M | WS4-5, WS5-2 | |
| 44 | Implement Report Upload by Lab Tech | [WS4](workstreams/04-core-api.md) | M | WS4-4, WS3-10 | |
| 45 | Implement Consent-Based Access (Doctor Grants) | [WS3](workstreams/03-authentication-authorization.md) | XL | WS3-4, WS2-1 | |
| 46 | Implement Access Grant Endpoints | [WS4](workstreams/04-core-api.md) | L | WS3-6 | |
| 47 | Implement Emergency Contact Endpoints | [WS4](workstreams/04-core-api.md) | M | WS4-3 | |
| 48 | Implement Aadhaar Validation Flow (Lambda) | [WS3](workstreams/03-authentication-authorization.md) | XL | WS3-1, WS2-6 | |

**Phase 3 Exit Criteria:**
- Users can sign up (email, phone, social, Aadhaar validated)
- JWT auth working with RBAC
- Reports uploaded, listed, viewed with signed URLs
- Doctor access grants create/revoke working
- Lab technicians can upload for patients

---

## Phase 4: Security, Compliance & File Processing (Weeks 10-13)

> Goal: Audit logging, encryption key rotation, virus scanning, compliance features.

| # | Task | Workstream | Complexity | Dependencies | Status |
|---|------|-----------|------------|-------------|--------|
| 49 | Configure TLS 1.3 Everywhere | [WS6](workstreams/06-security-compliance.md) | M | WS1-1 | |
| 50 | Implement CORS & Security Headers | [WS6](workstreams/06-security-compliance.md) | S | WS4-1 | |
| 51 | Implement Input Sanitization | [WS6](workstreams/06-security-compliance.md) | M | WS4-1 | |
| 52 | Implement Audit Logging Service | [WS6](workstreams/06-security-compliance.md) | L | WS4-1 | |
| 53 | Configure Audit Log Retention & Archival | [WS6](workstreams/06-security-compliance.md) | M | WS6-2 | |
| 54 | Implement Data Encryption Key Rotation | [WS6](workstreams/06-security-compliance.md) | L | WS2-5 | |
| 55 | Implement Consent Management (DPDP Act) | [WS6](workstreams/06-security-compliance.md) | L | WS4-3 | |
| 56 | Implement Virus Scanning Pipeline | [WS5](workstreams/05-file-storage-cdn.md) | L | WS5-3, WS1-4 | |
| 57 | Implement File Deletion (Soft Delete) | [WS5](workstreams/05-file-storage-cdn.md) | M | WS5-1 | |
| 58 | Configure CloudFront Distribution | [WS5](workstreams/05-file-storage-cdn.md) | M | WS5-1 | |
| 59 | Write Unit Tests for Domain Logic | [WS9](workstreams/09-advanced-features.md) | L | WS9-10 | |
| 60 | Write Integration Tests for Core API | [WS9](workstreams/09-advanced-features.md) | XL | WS9-10, all WS4 | |

**Phase 4 Exit Criteria:**
- All PII access logged with tamper-proof archival
- Encryption key rotation tested
- Uploaded files scanned for viruses
- CloudFront serving reports
- DPDP Act consent management working
- Integration test coverage for all API endpoints

---

## Phase 5: Advanced Features & Notifications (Weeks 13-16)

> Goal: Emergency access, push/email/SMS notifications, caching layer.

| # | Task | Workstream | Complexity | Dependencies | Status |
|---|------|-----------|------------|-------------|--------|
| 61 | Implement Push Notification Service (FCM) | [WS9](workstreams/09-advanced-features.md) | L | WS4-1 | |
| 62 | Implement Email Notification Service (SES) | [WS9](workstreams/09-advanced-features.md) | M | WS1-4 | |
| 63 | Implement SMS Notification Service (SNS) | [WS9](workstreams/09-advanced-features.md) | M | WS1-4 | |
| 64 | Implement Notification Preferences | [WS9](workstreams/09-advanced-features.md) | M | WS9-4, 5, 6 | |
| 65 | Implement Emergency Access Request Flow | [WS9](workstreams/09-advanced-features.md) | XL | WS3-6, WS4-9 | |
| 66 | Implement Emergency Access Auto-Expiry | [WS9](workstreams/09-advanced-features.md) | M | WS9-1 | |
| 67 | Implement Emergency Access Audit Trail | [WS9](workstreams/09-advanced-features.md) | M | WS9-1, WS6-2 | |
| 68 | Implement Redis Caching Layer | [WS9](workstreams/09-advanced-features.md) | L | WS1-2, WS4-3, WS4-5 | |
| 69 | Implement Response Caching Middleware | [WS9](workstreams/09-advanced-features.md) | M | WS4-5 | |
| 70 | Implement Breach Notification System | [WS6](workstreams/06-security-compliance.md) | L | WS6-2 | |
| 71 | Implement Data Export & Deletion | [WS6](workstreams/06-security-compliance.md) | L | WS4-3, WS2-5 | |

**Phase 5 Exit Criteria:**
- Emergency contacts can request break-glass access
- Push, email, SMS notifications working
- Redis caching reduces DB load
- Data export and deletion operational

---

## Phase 6: Web Frontends (Weeks 14-20)

> Goal: Patient portal and admin dashboard deployed.
> Note: Can be parallelized with Phase 5 — frontends depend on API (Phase 3) not Phase 5.

| # | Task | Workstream | Complexity | Dependencies | Status |
|---|------|-----------|------------|-------------|--------|

### Patient Portal (Next.js)

| 72 | Scaffold Next.js 14 Project | [WS7](workstreams/07-web-frontends.md) | M | None | |
| 73 | Implement Authentication Flow | [WS7](workstreams/07-web-frontends.md) | L | WS7-1, WS3-1 | |
| 74 | Implement Report Listing Page | [WS7](workstreams/07-web-frontends.md) | L | WS7-2, WS4-5 | |
| 75 | Implement Report Detail View | [WS7](workstreams/07-web-frontends.md) | L | WS7-3, WS4-6 | |
| 76 | Implement Report Upload Flow | [WS7](workstreams/07-web-frontends.md) | L | WS7-2, WS4-4 | |
| 77 | Implement Doctor Access Management | [WS7](workstreams/07-web-frontends.md) | L | WS7-2, WS4-8 | |
| 78 | Implement Emergency Contact Management | [WS7](workstreams/07-web-frontends.md) | M | WS7-2, WS4-9 | |
| 79 | Implement WCAG 2.1 AA Compliance | [WS7](workstreams/07-web-frontends.md) | L | WS7-3 through 7 | |
| 80 | Implement PWA Support | [WS7](workstreams/07-web-frontends.md) | M | WS7-3, 4 | |
| 81 | Implement Medical Charts/Vitals | [WS7](workstreams/07-web-frontends.md) | M | WS7-4 | |

### Admin Dashboard (Refine)

| 82 | Scaffold Refine + Material-UI Project | [WS7](workstreams/07-web-frontends.md) | M | None | |
| 83 | Implement User Management Module | [WS7](workstreams/07-web-frontends.md) | L | WS7-11, WS4-3 | |
| 84 | Implement Audit Log Viewer | [WS7](workstreams/07-web-frontends.md) | L | WS7-11, WS6-2 | |
| 85 | Implement System Monitoring Dashboard | [WS7](workstreams/07-web-frontends.md) | M | WS7-11, WS4-11 | |
| 86 | Implement Report Moderation Module | [WS7](workstreams/07-web-frontends.md) | M | WS7-11, WS4-5 | |

---

## Phase 7: Production Readiness (Weeks 18-22)

> Goal: Infrastructure provisioned, deployed, security hardened, load tested.

| # | Task | Workstream | Complexity | Dependencies | Status |
|---|------|-----------|------------|-------------|--------|
| 87 | Provision AWS Infrastructure (Terraform) | [WS8](workstreams/08-infrastructure-cicd.md) | XL | None | |
| 88 | Configure ALB with SSL | [WS8](workstreams/08-infrastructure-cicd.md) | M | WS8-4 | |
| 89 | Configure ECS Service | [WS8](workstreams/08-infrastructure-cicd.md) | L | WS8-2, 4 | |
| 90 | Configure RDS PostgreSQL | [WS8](workstreams/08-infrastructure-cicd.md) | M | WS8-4 | |
| 91 | Configure Route 53 DNS | [WS8](workstreams/08-infrastructure-cicd.md) | S | WS8-4, 5 | |
| 92 | Configure CloudWatch Monitoring | [WS8](workstreams/08-infrastructure-cicd.md) | M | WS8-4, 6 | |
| 93 | Configure WAF | [WS8](workstreams/08-infrastructure-cicd.md) | M | WS8-5 | |
| 94 | Set Up Staging Environment | [WS8](workstreams/08-infrastructure-cicd.md) | L | WS8-4 | |
| 95 | Configure Secrets Management | [WS8](workstreams/08-infrastructure-cicd.md) | M | WS8-4 | |
| 96 | Configure GitHub Actions CD Pipeline | [WS8](workstreams/08-infrastructure-cicd.md) | L | WS8-1, 2 | |
| 97 | Security Penetration Testing | [WS6](workstreams/06-security-compliance.md) | M | all WS4 | |
| 98 | Set Up Playwright E2E Tests | [WS9](workstreams/09-advanced-features.md) | L | WS7-1 through 6 | |
| 99 | Implement Load Testing | [WS9](workstreams/09-advanced-features.md) | L | all WS4 | |

**Phase 7 Exit Criteria:**
- Infrastructure fully provisioned via Terraform
- API deployed to ECS Fargate via CI/CD
- WAF + monitoring + alerting active
- No critical/high security vulnerabilities
- Load tests pass for Phase 1 scaling targets

---

## Dependency Graph (Critical Path)

```
WS1-1 (Solution) ──┬── WS1-2 (Aspire) ── WS1-4 (LocalStack) ──┬── WS3-1 (Cognito)
                    │                                             │── WS5-1 (S3 Bucket)
                    ├── WS2-2 (EF Core) ── WS2-3 (Entities) ─────┤── WS2-4 (Migration)
                    │                                             │── WS2-5 (PII Encrypt) ── WS2-6 (Aadhaar Vault)
                    │                                             │── WS2-7 (Repository)
                    ├── WS4-1 (API Structure) ── WS4-2 (Swagger)
                    │                          └─ WS4-10 (Validation)
                    └── WS8-1 (CI) ── WS8-3 (CD)

WS3-1 (Cognito) ── WS3-3 (JWT) ── WS3-4 (RBAC) ── WS3-6 (Consent) ── WS4-8 (Grant Endpoints)
                                                                      └── WS9-1 (Emergency Access)

WS2-6 (Aadhaar Vault) ── WS3-7 (Aadhaar Lambda)
```

**Longest Critical Path:**
WS1-1 → WS1-2 → WS1-4 → WS3-1 → WS3-3 → WS3-4 → WS3-6 → WS4-8 → WS9-1

---

## Parallelization Opportunities

| Parallel Track A | Parallel Track B | Parallel Track C |
|-----------------|-----------------|-----------------|
| WS1 (Dev Setup) | WS2-1 (Schema Design) | WS8-4 (Terraform) |
| WS2 (Data Layer) | WS7-1, WS7-11 (Frontend Scaffolding) | — |
| WS3 + WS4 (Auth + API) | WS5 (File Storage) | WS6-1, 5, 6 (Security basics) |
| WS6 (Compliance) | WS7 (Frontends) | WS9-10 (Test Infra) |
| WS9-1-3 (Emergency) | WS9-4-7 (Notifications) | WS9-8-9 (Caching) |

---

## Assumptions (Global)

1. **Solo developer** building MVP — parallelization applies when hiring
2. **AWS Activate credits** cover infrastructure costs for 6+ years at MVP scale
3. **.NET 8 + C#** is the backend language (founder's strongest skill)
4. **PostgreSQL** is the primary database with JSONB for flexible report data
5. **AWS Mumbai region** for India data residency compliance
6. **Aadhaar e-KYC provider** (DigiLocker vs NSDL) decided during implementation
7. **Mobile apps** (Swift + Kotlin) built separately, consuming the same API

---

## Open Questions

- Aadhaar e-KYC provider: DigiLocker API vs NSDL/CDSL?
- Notification channels: SMS + email + push, or subset? (SMS cost: ~₹0.40-0.85/msg)
- Deployment model: Start with ECS containers, or explore Lambda + Native AOT?
