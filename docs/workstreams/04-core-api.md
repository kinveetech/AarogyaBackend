# Workstream 4: Core API

## Overview
Implement the core REST API endpoints for user management, report management, and access control using ASP.NET Core 8 Minimal APIs.

## Tasks

### 1. Set Up Minimal API Structure
**Description:** Configure API versioning (URL prefix /api/v1/), global error handling, request/response logging middleware, CORS.

**Acceptance Criteria:**
- API responds on /api/v1/
- Errors return consistent ProblemDetails format
- CORS configured for frontend origins

**Complexity:** M | **Dependencies:** WS1-1

---

### 2. Configure Swagger/OpenAPI Documentation
**Description:** Set up Swashbuckle or NSwag for auto-generated API docs. Include JWT auth in Swagger UI.

**Acceptance Criteria:**
- /swagger shows all endpoints
- "Authorize" button works with JWT
- Request/response schemas documented

**Complexity:** S | **Dependencies:** 1

---

### 3. Implement User Profile Endpoints
**Description:** GET /api/v1/users/me, PUT /api/v1/users/me. Return user profile (decrypted PII). Update profile fields.

**Acceptance Criteria:**
- Authenticated user can read/update own profile
- PII decrypted in response
- Validation on updates

**Complexity:** M | **Dependencies:** WS2-7, WS3-3

---

### 4. Implement Report Upload Endpoint
**Description:** POST /api/v1/reports. Accept multipart form data (PDF/image + structured JSON metadata). Validate file type/size. Queue for processing via SQS.

**Acceptance Criteria:**
- File uploaded to S3, metadata saved to DB
- SQS message enqueued
- Returns 202 Accepted

**Complexity:** L | **Dependencies:** WS2-3, WS5-1

---

### 5. Implement Report Listing Endpoint
**Description:** GET /api/v1/reports. Paginated, filterable by report_type, date range, status. Role-based: patients see own, doctors see granted only.

**Acceptance Criteria:**
- Pagination works, filters applied
- Role-based filtering enforced
- JSONB parameters searchable

**Complexity:** L | **Dependencies:** WS2-8, WS3-6

---

### 6. Implement Report Detail Endpoint
**Description:** GET /api/v1/reports/{id}. Return structured report data + signed S3 URL for file download.

**Acceptance Criteria:**
- Returns full report with JSONB results
- Signed URL valid for 15 min
- Access control enforced

**Complexity:** M | **Dependencies:** 5, WS5-2

---

### 7. Implement Report Upload by Lab Technician
**Description:** POST /api/v1/labs/reports. Lab tech uploads report for a patient (by patient reference).

**Acceptance Criteria:**
- Lab tech can upload for any patient
- Patient notified, report status = "pending"

**Complexity:** M | **Dependencies:** 4, WS3-10

---

### 8. Implement Access Grant Endpoints
**Description:** POST /api/v1/access-grants (grant), DELETE /api/v1/access-grants/{id} (revoke), GET /api/v1/access-grants (list).

**Acceptance Criteria:**
- Patient can grant/revoke/list
- Doctor sees reports only within grant scope
- Expiry enforced

**Complexity:** L | **Dependencies:** WS3-6

---

### 9. Implement Emergency Contact Endpoints
**Description:** CRUD for /api/v1/emergency-contacts. Patient manages emergency contacts.

**Acceptance Criteria:**
- Patient can add/update/remove emergency contacts
- Max 3 contacts enforced

**Complexity:** M | **Dependencies:** 3

---

### 10. Implement Request Validation with FluentValidation
**Description:** Validators for all request DTOs. Consistent validation error format.

**Acceptance Criteria:**
- Invalid requests return 400 with field-level errors
- All endpoints validated

**Complexity:** M | **Dependencies:** 1

---

### 11. Implement Health Check Endpoints
**Description:** GET /health (liveness), GET /health/ready (readiness — checks DB, S3, SQS connectivity).

**Acceptance Criteria:**
- Health endpoints return appropriate status
- Used by ALB for routing

**Complexity:** S | **Dependencies:** WS2-9

---

### 12. Implement Rate Limiting
**Description:** Configure rate limiting middleware per endpoint/role.

**Acceptance Criteria:**
- Excessive requests return 429
- Rate limits configurable per environment

**Complexity:** M | **Dependencies:** 1

---

## Assumptions
- Minimal APIs preferred over controllers
- API versioning via URL prefix (/api/v1/) from day 1
- ProblemDetails for error responses
- FluentValidation for request validation
- File upload max: 25 MB (configurable)

## Risks
- Minimal APIs may have limitations for complex scenarios — controllers as fallback
- Large file uploads may need streaming to avoid memory pressure
- JSONB query performance needs monitoring as data grows
