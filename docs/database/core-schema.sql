-- Core schema proposal for WS2/Issue #11
-- PostgreSQL 16+

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;

DO $$ BEGIN
  CREATE TYPE user_role AS ENUM ('patient', 'doctor', 'lab_technician', 'admin');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE report_type AS ENUM ('blood_test', 'urine_test', 'radiology', 'cardiology', 'other');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE report_status AS ENUM ('draft', 'uploaded', 'processing', 'validated', 'published', 'archived');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE access_grant_status AS ENUM ('active', 'revoked', 'expired');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

CREATE TABLE IF NOT EXISTS users (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  external_auth_id TEXT UNIQUE,
  role user_role NOT NULL,

  first_name_encrypted BYTEA NOT NULL,
  last_name_encrypted BYTEA NOT NULL,
  email_encrypted BYTEA NOT NULL,
  phone_encrypted BYTEA,
  date_of_birth DATE,
  gender TEXT,

  -- Search-friendly hashed fields for encrypted PII
  email_hash BYTEA UNIQUE,
  phone_hash BYTEA,

  -- Aadhaar vault linkage (plaintext Aadhaar never stored here)
  aadhaar_ref_token UUID UNIQUE,
  aadhaar_sha256 BYTEA UNIQUE,

  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  CONSTRAINT users_gender_chk CHECK (gender IS NULL OR gender IN ('male', 'female', 'other', 'unknown'))
);

CREATE TABLE IF NOT EXISTS reports (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  report_number TEXT NOT NULL UNIQUE,
  patient_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
  doctor_id UUID REFERENCES users(id) ON DELETE SET NULL,
  uploaded_by_user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,

  report_type report_type NOT NULL,
  status report_status NOT NULL DEFAULT 'uploaded',

  source_system TEXT,
  collected_at TIMESTAMPTZ,
  reported_at TIMESTAMPTZ,
  uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  file_storage_key TEXT,
  checksum_sha256 TEXT,

  -- Flexible medical payload
  results JSONB NOT NULL DEFAULT '{}'::jsonb,
  metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  CONSTRAINT reports_time_order_chk CHECK (reported_at IS NULL OR collected_at IS NULL OR reported_at >= collected_at)
);

CREATE TABLE IF NOT EXISTS report_parameters (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  report_id UUID NOT NULL REFERENCES reports(id) ON DELETE CASCADE,

  parameter_code TEXT NOT NULL,
  parameter_name TEXT NOT NULL,
  measured_value_text TEXT,
  measured_value_numeric NUMERIC(18,6),
  unit TEXT,
  reference_range_text TEXT,
  is_abnormal BOOLEAN,

  -- Optional JSONB blob for lab-specific shape/extensions
  raw_parameter JSONB NOT NULL DEFAULT '{}'::jsonb,

  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT report_parameters_unique_per_report UNIQUE (report_id, parameter_code)
);

CREATE TABLE IF NOT EXISTS access_grants (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  patient_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  granted_to_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  granted_by_user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,

  grant_reason TEXT,
  scope JSONB NOT NULL DEFAULT '{}'::jsonb,
  status access_grant_status NOT NULL DEFAULT 'active',

  starts_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  expires_at TIMESTAMPTZ,
  revoked_at TIMESTAMPTZ,

  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  CONSTRAINT access_grants_time_window_chk CHECK (expires_at IS NULL OR expires_at > starts_at),
  CONSTRAINT access_grants_revoked_time_chk CHECK (revoked_at IS NULL OR revoked_at >= starts_at),
  CONSTRAINT access_grants_not_self_chk CHECK (patient_id <> granted_to_user_id)
);

CREATE TABLE IF NOT EXISTS emergency_contacts (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,

  name_encrypted BYTEA NOT NULL,
  relationship TEXT NOT NULL,
  phone_encrypted BYTEA NOT NULL,
  phone_hash BYTEA,

  is_primary BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS audit_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

  actor_user_id UUID REFERENCES users(id) ON DELETE SET NULL,
  actor_role user_role,
  action TEXT NOT NULL,

  entity_type TEXT NOT NULL,
  entity_id UUID,

  correlation_id UUID,
  request_path TEXT,
  request_method TEXT,
  client_ip INET,
  user_agent TEXT,

  result_status INTEGER,
  details JSONB NOT NULL DEFAULT '{}'::jsonb,

  CONSTRAINT audit_logs_result_status_chk CHECK (result_status IS NULL OR (result_status BETWEEN 100 AND 599))
);

-- Indexing strategy
CREATE INDEX IF NOT EXISTS ix_users_role_active ON users(role, is_active);
CREATE INDEX IF NOT EXISTS ix_users_email_hash ON users(email_hash);
CREATE INDEX IF NOT EXISTS ix_users_phone_hash ON users(phone_hash);
CREATE INDEX IF NOT EXISTS ix_users_aadhaar_sha256 ON users(aadhaar_sha256);

CREATE INDEX IF NOT EXISTS ix_reports_patient_uploaded_at ON reports(patient_id, uploaded_at DESC);
CREATE INDEX IF NOT EXISTS ix_reports_status_uploaded_at ON reports(status, uploaded_at DESC);
CREATE INDEX IF NOT EXISTS ix_reports_type_reported_at ON reports(report_type, reported_at DESC);
CREATE INDEX IF NOT EXISTS ix_reports_results_gin ON reports USING GIN (results jsonb_path_ops);
CREATE INDEX IF NOT EXISTS ix_reports_metadata_gin ON reports USING GIN (metadata jsonb_path_ops);

CREATE INDEX IF NOT EXISTS ix_report_parameters_report_id ON report_parameters(report_id);
CREATE INDEX IF NOT EXISTS ix_report_parameters_name ON report_parameters(parameter_name);
CREATE INDEX IF NOT EXISTS ix_report_parameters_value_numeric ON report_parameters(measured_value_numeric)
  WHERE measured_value_numeric IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_report_parameters_raw_parameter_gin ON report_parameters USING GIN (raw_parameter jsonb_path_ops);

CREATE INDEX IF NOT EXISTS ix_access_grants_patient_status ON access_grants(patient_id, status);
CREATE INDEX IF NOT EXISTS ix_access_grants_granted_to_status ON access_grants(granted_to_user_id, status);
CREATE INDEX IF NOT EXISTS ix_access_grants_scope_gin ON access_grants USING GIN (scope jsonb_path_ops);
CREATE UNIQUE INDEX IF NOT EXISTS ux_access_grants_active
  ON access_grants(patient_id, granted_to_user_id)
  WHERE status = 'active';

CREATE INDEX IF NOT EXISTS ix_emergency_contacts_user_id ON emergency_contacts(user_id);
CREATE INDEX IF NOT EXISTS ix_emergency_contacts_phone_hash ON emergency_contacts(phone_hash);
CREATE UNIQUE INDEX IF NOT EXISTS ux_emergency_contacts_primary_per_user
  ON emergency_contacts(user_id)
  WHERE is_primary;

CREATE INDEX IF NOT EXISTS ix_audit_logs_occurred_at ON audit_logs(occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_actor_time ON audit_logs(actor_user_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity_time ON audit_logs(entity_type, entity_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_details_gin ON audit_logs USING GIN (details jsonb_path_ops);
