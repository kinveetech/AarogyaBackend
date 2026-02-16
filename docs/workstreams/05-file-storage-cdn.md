# Workstream 5: File Storage & CDN

## Overview
Implement secure file storage for medical reports using AWS S3, signed URL generation for secure access, virus scanning for uploads, and CloudFront CDN integration.

## Tasks

### 1. Configure S3 Bucket for Reports
**Description:** Create S3 bucket with server-side encryption (SSE-KMS), versioning, lifecycle policies (move to Glacier after 1 year). Block all public access.

**Acceptance Criteria:**
- Bucket created, encryption enabled
- Public access blocked
- Lifecycle policy active (Glacier after 1 year)

**Complexity:** M | **Dependencies:** WS1-4

---

### 2. Implement Signed URL Generation
**Description:** Generate pre-signed S3 URLs for secure file download. Configurable expiry (15 min for doctors, 60 min for patients).

**Acceptance Criteria:**
- Signed URLs work, expire correctly
- Unauthorized access returns 403

**Complexity:** M | **Dependencies:** 1

---

### 3. Implement File Upload Service
**Description:** Accept file uploads (PDF, JPG, PNG, DICOM), validate file type and size (max 25MB), upload to S3 with metadata tags.

**Acceptance Criteria:**
- Files uploaded with correct content-type
- Metadata tags applied
- File type/size validation enforced

**Complexity:** M | **Dependencies:** 1

---

### 4. Implement Virus Scanning Pipeline
**Description:** SQS-triggered Lambda or background service scanning uploads using ClamAV or GuardDuty for S3. Quarantine infected files.

**Acceptance Criteria:**
- Every upload scanned
- Infected files quarantined
- Clean files marked as verified

**Complexity:** L | **Dependencies:** 3, WS1-4

---

### 5. Configure CloudFront Distribution
**Description:** Set up CloudFront with S3 origin, OAI (Origin Access Identity), HTTPS only.

**Acceptance Criteria:**
- Files served via CloudFront
- Direct S3 access blocked
- HTTPS enforced

**Complexity:** M | **Dependencies:** 1

---

### 6. Implement File Deletion (Soft Delete)
**Description:** Soft delete reports (mark as deleted, retain in S3 for compliance period). Hard delete after 7-year retention via lifecycle policy.

**Acceptance Criteria:**
- Deleted reports not accessible via API
- Retained in S3 for 7 years, auto-purged after

**Complexity:** M | **Dependencies:** 1

---

### 7. Implement File Checksum Verification
**Description:** Compute SHA-256 checksum on upload, store in DB, verify on download.

**Acceptance Criteria:**
- Checksum computed and stored
- Tampered files detected on download

**Complexity:** S | **Dependencies:** 3

---

### 8. Configure S3 Event Notifications
**Description:** S3 event notifications to SQS on file upload. Triggers report processing pipeline.

**Acceptance Criteria:**
- File upload triggers SQS message
- Processing pipeline invoked

**Complexity:** S | **Dependencies:** 1, WS1-4

---

## Assumptions
- LocalStack S3 sufficient for local development
- ClamAV container for local virus scanning, GuardDuty for production
- DICOM support is basic (store/retrieve) — no parsing in MVP
- CloudFront not needed for local dev

## Risks
- Virus scanning adds latency — design async flow
- Large DICOM files may exceed 25MB — may need increase for imaging
- CloudFront signed URLs vs S3 signed URLs — choose one approach for consistency
