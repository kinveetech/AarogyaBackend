# Workstream 2: Database & Data Layer

## Overview
Design and implement the PostgreSQL database schema, EF Core data access layer, Aadhaar Data Vault, and field-level encryption for PII.

## Tasks

### 1. Design Core Database Schema
**Description:** Design tables: users, test_reports, access_grants, emergency_contacts, audit_logs. Include ENUM types for roles, report_type, status.

**Design Artifacts:**
- `docs/database/core-schema.md`
- `docs/database/core-schema.sql`

**Acceptance Criteria:**
- ERD documented, schema SQL reviewed
- All tables, relationships, and constraints defined
- ENUM types for roles, report_type, status

**Complexity:** L | **Dependencies:** None

---

### 2. Configure EF Core with PostgreSQL
**Description:** Set up Npgsql provider, DbContext, connection string management. Enable JSONB support.

**Acceptance Criteria:**
- DbContext connects to PostgreSQL
- JSONB support enabled and verified
- Basic CRUD operations work

**Complexity:** M | **Dependencies:** WS1-1

---

### 3. Create EF Core Entity Models
**Description:** Map all tables to C# entities. Configure JSONB column for test_reports.results. Use value converters for encrypted fields.

**Acceptance Criteria:**
- All entities mapped (User, TestReport, AccessGrant, EmergencyContact, AuditLog)
- JSONB column configured for results
- `dotnet ef migrations add` succeeds

**Complexity:** L | **Dependencies:** 1, 2

---

### 4. Implement Initial Migration
**Description:** Generate and apply initial migration. Seed ENUM types and reference data.

**Acceptance Criteria:**
- `dotnet ef database update` creates all tables
- pgcrypto extension enabled
- Migration is idempotent and rollback-safe

**Complexity:** M | **Dependencies:** 3

---

### 5. Implement Field-Level PII Encryption
**Description:** Use pgcrypto or application-level encryption (ASP.NET Data Protection API + AWS KMS) for name, phone, email, address columns.

**Acceptance Criteria:**
- PII columns encrypted at rest, decrypted on read
- Key rotation supported via AWS KMS
- Works in both LocalStack (dev) and real AWS (prod)

**Complexity:** XL | **Dependencies:** 3, WS1-4

---

### 6. Design Aadhaar Data Vault
**Description:** Separate encrypted table/schema for Aadhaar numbers. SHA-256 hash in users table for lookup, UUID reference token, AES-256 encrypted Aadhaar in vault.

**Acceptance Criteria:**
- Aadhaar stored encrypted (AES-256), lookup by SHA-256 hash works
- UUID reference token links users table to vault
- No plaintext Aadhaar stored anywhere
- Compliant with UIDAI ADV mandate

**Complexity:** XL | **Dependencies:** 5

---

### 7. Implement Repository Pattern
**Description:** Create IRepository<T> and concrete implementations for each entity. Include specification pattern for complex queries.

**Acceptance Criteria:**
- All CRUD operations go through repositories
- Unit testable, DI configured
- No direct DbContext usage outside repositories

**Complexity:** L | **Dependencies:** 3

---

### 8. Configure JSONB Indexing
**Description:** Create GIN indexes on test_reports.results for querying report parameters (e.g., hemoglobin > X).

**Acceptance Criteria:**
- JSONB queries use indexes (EXPLAIN shows index scan)
- Common query patterns optimized
- Migration includes index creation

**Complexity:** M | **Dependencies:** 4

---

### 9. Set Up Database Health Checks
**Description:** EF Core health check for connection monitoring.

**Acceptance Criteria:**
- /health endpoint reports DB status
- Unhealthy state reported when DB unavailable

**Complexity:** S | **Dependencies:** 2

---

### 10. Create Seed Data for Development
**Description:** Seed test users (patient, doctor, lab tech, admin), sample reports with JSONB data, sample access grants.

**Acceptance Criteria:**
- All roles represented in seed data
- Realistic JSONB report data
- Environment-specific (dev only)

**Complexity:** M | **Dependencies:** 4

---

## Assumptions
- PostgreSQL 16 with pgcrypto extension
- AWS KMS via LocalStack for local dev, real KMS in production
- Aadhaar Data Vault is a separate schema within same PostgreSQL instance at startup
- EF Core 8 + Npgsql supports all required JSONB operations

## Risks
- Field-level encryption prevents WHERE clauses on encrypted columns — need searchable hashes
- Aadhaar Data Vault compliance requirements may evolve
- JSONB indexing performance needs validation with realistic data volumes
