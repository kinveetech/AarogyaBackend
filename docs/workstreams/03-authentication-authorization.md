# Workstream 3: Authentication & Authorization

## Overview
Implement multi-provider authentication via AWS Cognito, role-based access control (RBAC), consent-based access for doctor-patient relationships, and custom Aadhaar validation flow.

## Tasks

### 1. Configure AWS Cognito User Pool
**Description:** Set up user pool with email + phone sign-up, password policies, MFA configuration. Configure in LocalStack for dev, real Cognito for staging/prod.

**Acceptance Criteria:**
- User pool created with email and phone attributes
- Password policies and MFA configured
- Sign-up/sign-in flows work via AWS CLI

**Complexity:** M | **Dependencies:** WS1-4

---

### 2. Integrate Social Identity Providers
**Description:** Configure Google, Apple, Facebook as federated identity providers in Cognito.

**Acceptance Criteria:**
- All 3 social providers configured
- Users can sign up/in with social accounts
- Cognito tokens issued correctly

**Complexity:** L | **Dependencies:** 1

---

### 3. Implement JWT Validation Middleware
**Description:** ASP.NET Core authentication middleware validating Cognito JWT tokens. Extract claims (sub, email, roles).

**Acceptance Criteria:**
- Invalid/expired tokens rejected with 401
- Valid tokens parsed to ClaimsPrincipal
- Claims include sub, email, custom roles

**Complexity:** M | **Dependencies:** 1

---

### 4. Implement RBAC (Role-Based Access Control)
**Description:** Define roles: Patient, Doctor, LabTechnician, Admin. Cognito groups for role assignment. Authorization policies in ASP.NET Core.

**Acceptance Criteria:**
- Each role can only access permitted endpoints
- Unauthorized returns 403
- Policies enforced across all protected endpoints

**Complexity:** L | **Dependencies:** 3

---

### 5. Implement OAuth2 PKCE Flow
**Description:** Support Authorization Code + PKCE for native iOS/Android apps. No client secret.

**Acceptance Criteria:**
- PKCE flow works end-to-end
- Native apps complete full auth flow
- Tokens stored securely

**Complexity:** M | **Dependencies:** 1

---

### 6. Implement Consent-Based Access (Doctor Access Grants)
**Description:** Patient creates access_grant (doctor_id, report_ids[], expiry). Doctor views only granted reports within time window. Patient can revoke at any time.

**Acceptance Criteria:**
- Doctor sees only granted reports
- Revocation is immediate
- Expired grants denied
- Authorization integrated with report endpoints

**Complexity:** XL | **Dependencies:** 4, WS2-1

---

### 7. Implement Aadhaar Validation Flow (Cognito Lambda Trigger)
**Description:** Pre-sign-up Lambda trigger: calls UIDAI e-KYC API, validates Aadhaar, computes SHA-256 hash, stores in Aadhaar Data Vault, returns UUID reference token.

**Acceptance Criteria:**
- Sign-up fails without valid Aadhaar
- Hash stored in users table, encrypted number in vault
- UUID reference token generated
- Error handling for e-KYC API failures

**Complexity:** XL | **Dependencies:** 1, WS2-6

---

### 8. Implement Phone OTP Verification
**Description:** Cognito SMS-based verification for phone number during sign-up. SNS for SMS delivery.

**Acceptance Criteria:**
- OTP sent via SMS, verification works
- Verified phone stored in user profile

**Complexity:** M | **Dependencies:** 1

---

### 9. Implement Token Refresh Flow
**Description:** Refresh token handling, token rotation, revocation on logout.

**Acceptance Criteria:**
- Expired access tokens refreshed silently
- Logout invalidates refresh token
- Token rotation implemented

**Complexity:** M | **Dependencies:** 3

---

### 10. Implement API Key Authentication (Lab Systems)
**Description:** API key auth for lab system integrations (machine-to-machine).

**Acceptance Criteria:**
- Lab systems authenticate via API key
- Scoped to upload-only endpoints
- Rate limited, rotation available

**Complexity:** M | **Dependencies:** 3

---

## Assumptions
- AWS Cognito is single source of truth for authentication
- Aadhaar e-KYC provider (DigiLocker or NSDL) decided during implementation — design provider-agnostic interface
- Social login requires Google, Apple, Facebook developer accounts
- LocalStack Cognito emulation may have limitations

## Risks
- Cognito Lambda triggers have cold start latency
- Aadhaar e-KYC API availability is an external dependency
- Consent-based access is fully custom — no out-of-the-box solution
