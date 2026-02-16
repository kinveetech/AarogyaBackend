# Workstream 6: Security & Compliance

## Overview
Implement security layers (encryption in transit, at rest, field-level), audit logging, and compliance with Indian healthcare data regulations (DPDP Act, DISHA, UIDAI ADV, IT Act 2000).

## Tasks

### 1. Configure TLS 1.3 Everywhere
**Description:** HTTPS on ALB, enforce TLS 1.3 minimum between all services (API -> DB, API -> S3, API -> Redis).

**Acceptance Criteria:**
- No plaintext connections
- TLS 1.3 verified via SSL Labs or equivalent

**Complexity:** M | **Dependencies:** WS1-1

---

### 2. Implement Audit Logging Service
**Description:** Log every access to PII and medical data: who (user_id, role), what (action, resource), when (UTC + IST), where (IP, device fingerprint). Write to CloudWatch Logs.

**Acceptance Criteria:**
- All PII access logged
- Logs queryable
- No PII in log messages themselves

**Complexity:** L | **Dependencies:** WS4-1

---

### 3. Configure Audit Log Retention & Archival
**Description:** CloudWatch Logs -> S3 archive via subscription filter. 6+ year retention (DISHA requirement). Write-once policy.

**Acceptance Criteria:**
- Logs archived to S3
- Retention policy enforced (6+ years)
- Tamper-proof (S3 Object Lock)

**Complexity:** M | **Dependencies:** 2

---

### 4. Implement Data Encryption Key Rotation
**Description:** AWS KMS key rotation for field-level encryption keys. Re-encryption strategy for existing data.

**Acceptance Criteria:**
- Keys rotate automatically (annual)
- Re-encryption runs without downtime
- Old keys remain for decryption

**Complexity:** L | **Dependencies:** WS2-5

---

### 5. Implement CORS & Security Headers
**Description:** Strict CORS policy, CSP, HSTS, X-Frame-Options, X-Content-Type-Options.

**Acceptance Criteria:**
- Security headers present on all responses
- CORS blocks unauthorized origins

**Complexity:** S | **Dependencies:** WS4-1

---

### 6. Implement Input Sanitization
**Description:** Sanitize all user inputs against XSS, SQL injection. Use parameterized queries (EF Core default). HTML sanitization for user-provided text.

**Acceptance Criteria:**
- OWASP Top 10 input attacks blocked
- No raw SQL anywhere

**Complexity:** M | **Dependencies:** WS4-1

---

### 7. Implement Consent Management (DPDP Act)
**Description:** User consent collection and withdrawal mechanism. Purpose limitation for data processing. Data minimization enforcement.

**Acceptance Criteria:**
- Consent recorded before data processing
- Withdrawal stops processing
- Purpose tracked per data collection

**Complexity:** L | **Dependencies:** WS4-3

---

### 8. Implement Breach Notification System
**Description:** Automated detection of potential breaches (unusual access patterns, bulk data exports). Notification pipeline to users and authorities.

**Acceptance Criteria:**
- Suspicious patterns trigger alerts
- Notification sent within required timeframe

**Complexity:** L | **Dependencies:** 2

---

### 9. Implement Data Export & Deletion (Right to Erasure)
**Description:** User can request data export (DPDP Act). User can request account and data deletion with compliance retention exceptions.

**Acceptance Criteria:**
- Export returns all user data in portable format
- Deletion removes PII while retaining anonymized audit logs

**Complexity:** L | **Dependencies:** WS4-3, WS2-5

---

### 10. Security Penetration Testing Checklist
**Description:** OWASP ZAP scan configuration, API security testing, dependency vulnerability scanning (Dependabot/Snyk).

**Acceptance Criteria:**
- No critical/high vulnerabilities
- All findings documented and remediated

**Complexity:** M | **Dependencies:** All WS4 tasks

---

## Assumptions
- DPDP Act rules pending — implement based on enacted provisions, plan to update
- DISHA still draft — implement audit logging and encryption that would satisfy expected requirements
- UIDAI ADV mandate immediately applicable — full compliance at launch
- Breach notification to DPBI — exact process TBD when rules finalized

## Risks
- Regulatory landscape is evolving — compliance requirements may change
- Audit log volume at scale could be significant — cost management needed
- Encryption key rotation requires careful coordination to avoid data loss
- Breach detection requires baseline understanding of "normal" access patterns
