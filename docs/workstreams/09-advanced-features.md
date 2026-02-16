# Workstream 9: Advanced Features & Testing

## Overview
Implement emergency access (break-glass pattern), push notifications, caching layer, and comprehensive testing infrastructure.

## Tasks

### Emergency Access

### 1. Implement Emergency Access Request Flow
**Description:** Emergency contact submits access request. System grants temporary access (24-48 hours configurable). Patient notified via SMS + email + push.

**Acceptance Criteria:**
- Emergency contact can request access
- Temporary grant created
- Patient notified on all channels
- Full audit trail logged

**Complexity:** XL | **Dependencies:** WS3-6, WS4-9

---

### 2. Implement Emergency Access Auto-Expiry
**Description:** Background service that auto-expires emergency access grants. Notification before expiry.

**Acceptance Criteria:**
- Grants expire automatically
- Pre-expiry notification sent
- Expired grants deny access immediately

**Complexity:** M | **Dependencies:** 1

---

### 3. Implement Emergency Access Audit Trail
**Description:** Detailed logging of every action during emergency access: who requested, when granted, what accessed, when expired/revoked.

**Acceptance Criteria:**
- Complete audit trail for every emergency access
- Queryable by admin, tamper-proof

**Complexity:** M | **Dependencies:** 1, WS6-2

---

### Notifications

### 4. Implement Push Notification Service
**Description:** Firebase Cloud Messaging integration for iOS and Android. Device token registration.

**Acceptance Criteria:**
- Push notifications delivered to registered devices
- Both iOS and Android supported

**Complexity:** L | **Dependencies:** WS4-1

---

### 5. Implement Email Notification Service
**Description:** AWS SES for transactional emails (report uploaded, access granted, emergency access). HTML email templates.

**Acceptance Criteria:**
- Emails sent for all notification events
- HTML rendering correct, unsubscribe link included

**Complexity:** M | **Dependencies:** WS1-4

---

### 6. Implement SMS Notification Service
**Description:** AWS SNS for critical notifications (emergency access, OTP).

**Acceptance Criteria:**
- SMS delivered for critical events
- Cost tracking per message
- India carrier support verified

**Complexity:** M | **Dependencies:** WS1-4

---

### 7. Implement Notification Preferences
**Description:** User configurable notification preferences (which events, which channels).

**Acceptance Criteria:**
- Users can enable/disable notification types per channel
- Preferences respected

**Complexity:** M | **Dependencies:** 4, 5, 6

---

### Caching

### 8. Implement Redis Caching Layer
**Description:** Cache frequently accessed data: user profiles, report listings, access grants. Cache invalidation strategy.

**Acceptance Criteria:**
- Cache hit rate > 80% for repeated requests
- Cache invalidated on data changes
- TTL configured per entity type

**Complexity:** L | **Dependencies:** WS1-2, WS4-3, WS4-5

---

### 9. Implement Response Caching Middleware
**Description:** HTTP response caching with ETag support for report listings.

**Acceptance Criteria:**
- Repeated GET requests served from cache
- ETag-based conditional requests work
- Cache-control headers set

**Complexity:** M | **Dependencies:** WS4-5

---

### Testing

### 10. Set Up xUnit Test Infrastructure
**Description:** Configure xUnit with test fixtures, DI for tests, test database (Testcontainers).

**Acceptance Criteria:**
- `dotnet test` runs all tests
- Testcontainers starts PostgreSQL
- Tests isolated

**Complexity:** M | **Dependencies:** WS1-1, WS2-2

---

### 11. Write Integration Tests for Core API
**Description:** Tests for all API endpoints with real database (Testcontainers). Auth, RBAC, CRUD, file upload.

**Acceptance Criteria:**
- All endpoints covered
- Tests pass with real PostgreSQL
- Auth scenarios tested

**Complexity:** XL | **Dependencies:** 10, all WS4 tasks

---

### 12. Write Unit Tests for Domain Logic
**Description:** Tests for encryption/decryption, access grant validation, emergency access logic, consent management.

**Acceptance Criteria:**
- Domain logic 80%+ code coverage
- Edge cases covered

**Complexity:** L | **Dependencies:** 10

---

### 13. Set Up Playwright E2E Tests
**Description:** E2E tests for patient portal critical flows: login, view reports, grant access, upload report.

**Acceptance Criteria:**
- E2E tests run in CI
- Critical user flows verified
- Screenshot on failure

**Complexity:** L | **Dependencies:** WS7-1 through WS7-6

---

### 14. Implement Load Testing
**Description:** k6 or NBomber load tests simulating concurrent users. Target: API handles expected load at each phase.

**Acceptance Criteria:**
- Load test scripts for key endpoints
- Baseline performance documented
- Bottlenecks identified

**Complexity:** L | **Dependencies:** all WS4 tasks

---

## Assumptions
- Firebase Cloud Messaging free tier sufficient for push notifications
- AWS SES sandbox mode for dev, production access requires AWS approval
- Redis via ElastiCache in production, Docker locally
- Testcontainers requires Docker — compatible with CI environment
- Load testing targets defined per scaling phase in architecture plan

## Risks
- Emergency access is a critical safety feature — needs extensive testing and legal review
- Push notification delivery is not guaranteed — implement retry/fallback
- Cache invalidation is a hard problem — start with conservative TTLs
- Load testing environment needs to approximate production
