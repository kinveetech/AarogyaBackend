# Core Database Schema Design (Issue #11)

This document defines the proposed PostgreSQL schema for:
- `users`
- `reports`
- `report_parameters`
- `access_grants`
- `emergency_contacts`
- `audit_logs`

Supporting enum types, constraints, JSONB shape, and indexing strategy are included.

## ERD

```mermaid
erDiagram
  users ||--o{ reports : "patient_id"
  users ||--o{ reports : "uploaded_by_user_id"
  users ||--o{ reports : "doctor_id"
  users ||--o{ access_grants : "patient_id"
  users ||--o{ access_grants : "granted_to_user_id"
  users ||--o{ access_grants : "granted_by_user_id"
  users ||--o{ emergency_contacts : "user_id"
  users ||--o{ audit_logs : "actor_user_id"

  reports ||--o{ report_parameters : "report_id"

  users {
    uuid id PK
    text external_auth_id UK
    user_role role
    bytea first_name_encrypted
    bytea last_name_encrypted
    bytea email_encrypted
    bytea phone_encrypted
    bytea email_hash UK
    bytea phone_hash
    uuid aadhaar_ref_token UK
    bytea aadhaar_sha256 UK
    bool is_active
    timestamptz created_at
    timestamptz updated_at
  }

  reports {
    uuid id PK
    text report_number UK
    uuid patient_id FK
    uuid doctor_id FK
    uuid uploaded_by_user_id FK
    report_type report_type
    report_status status
    timestamptz collected_at
    timestamptz reported_at
    timestamptz uploaded_at
    jsonb results
    jsonb metadata
    timestamptz created_at
    timestamptz updated_at
  }

  report_parameters {
    uuid id PK
    uuid report_id FK
    text parameter_code
    text parameter_name
    numeric measured_value_numeric
    text measured_value_text
    text unit
    text reference_range_text
    bool is_abnormal
    jsonb raw_parameter
    timestamptz created_at
  }

  access_grants {
    uuid id PK
    uuid patient_id FK
    uuid granted_to_user_id FK
    uuid granted_by_user_id FK
    access_grant_status status
    timestamptz starts_at
    timestamptz expires_at
    timestamptz revoked_at
    jsonb scope
    timestamptz created_at
  }

  emergency_contacts {
    uuid id PK
    uuid user_id FK
    bytea name_encrypted
    text relationship
    bytea phone_encrypted
    bytea phone_hash
    bool is_primary
    timestamptz created_at
    timestamptz updated_at
  }

  audit_logs {
    uuid id PK
    timestamptz occurred_at
    uuid actor_user_id FK
    user_role actor_role
    text action
    text entity_type
    uuid entity_id
    uuid correlation_id
    text request_path
    text request_method
    inet client_ip
    text user_agent
    int result_status
    jsonb details
  }
```

## SQL Definition

Authoritative SQL DDL is in:
- `docs/database/core-schema.sql`

## JSONB Structure: `reports.results`

`reports.results` is intended for flexible, vendor-specific medical report payloads.

Canonical shape:

```json
{
  "reportVersion": 1,
  "lab": {
    "name": "ABC Diagnostics",
    "code": "ABC-DIAG"
  },
  "parameters": [
    {
      "code": "HGB",
      "name": "Hemoglobin",
      "value": 13.4,
      "unit": "g/dL",
      "referenceRange": "12.0-16.0",
      "abnormalFlag": false
    }
  ],
  "attachments": [
    {
      "type": "pdf",
      "storageKey": "reports/2026/02/report-123.pdf"
    }
  ],
  "notes": "Sample report"
}
```

Notes:
- `report_parameters` stores extracted/searchable values for high-frequency filtering and analytics.
- `results` preserves the full original payload for auditability and extensibility.

## Constraints and Data Integrity

Highlights:
- UUID PKs with `gen_random_uuid()`.
- Foreign keys with explicit delete behavior (`RESTRICT`, `SET NULL`, `CASCADE` by use-case).
- Time-order constraints for report timestamps and access grant validity windows.
- Partial unique index to enforce a single active access grant per patient-grantee pair.
- Partial unique index to enforce one primary emergency contact per user.
- Check constraint on HTTP status codes for audit logs.

## Indexing Strategy

### B-tree indexes
- Operational filters and ordering:
  - `reports(patient_id, uploaded_at desc)`
  - `reports(status, uploaded_at desc)`
  - `audit_logs(actor_user_id, occurred_at desc)`
  - `audit_logs(entity_type, entity_id, occurred_at desc)`

### Partial indexes
- Business rules and sparse high-value lookups:
  - `ux_access_grants_active` where `status = 'active'`
  - `ux_emergency_contacts_primary_per_user` where `is_primary`
  - `ix_report_parameters_value_numeric` where numeric value is present

### GIN indexes for JSONB
- `reports.results` and `reports.metadata` with `jsonb_path_ops`
- `report_parameters.raw_parameter`
- `access_grants.scope`
- `audit_logs.details`

This balances transactional queries, timeline queries, and JSON path search use-cases.

## Scope and Follow-ups

This issue defines schema design only. Follow-up implementation issues should cover:
- EF Core entity mappings and migrations
- seed data
- query plans (`EXPLAIN ANALYZE`) against realistic datasets
- encrypted field handling and Aadhaar vault implementation
