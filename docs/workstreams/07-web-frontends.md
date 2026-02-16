# Workstream 7: Web Frontends

## Overview
Build two web applications: a patient/doctor portal for viewing and managing medical reports, and an internal admin dashboard for system management.

## Tasks

### Patient/Doctor Portal (Next.js 14 + Chakra UI + TypeScript)

### 1. Scaffold Next.js 14 Project
**Description:** Initialize Next.js 14 with App Router, TypeScript, Chakra UI, ESLint, Prettier.

**Acceptance Criteria:**
- `npm run dev` starts, Chakra UI theme applied
- TypeScript strict mode enabled

**Complexity:** M | **Dependencies:** None

---

### 2. Implement Authentication Flow
**Description:** NextAuth.js + AWS Cognito integration. Login, register, social login (Google, Apple, Facebook), logout.

**Acceptance Criteria:**
- All auth flows work
- JWT stored securely, session management functional

**Complexity:** L | **Dependencies:** 1, WS3-1

---

### 3. Implement Report Listing Page
**Description:** Paginated report list with filters (type, date, status). Responsive design. TanStack Query for data fetching.

**Acceptance Criteria:**
- Reports load with pagination, filters work
- Loading/error states handled

**Complexity:** L | **Dependencies:** 2, WS4-5

---

### 4. Implement Report Detail View
**Description:** Structured data display (test parameters with flags), embedded PDF viewer, download via signed URL.

**Acceptance Criteria:**
- Report data renders correctly
- PDF viewer works, signed URL download functional

**Complexity:** L | **Dependencies:** 3, WS4-6

---

### 5. Implement Report Upload Flow
**Description:** Drag-and-drop file upload + structured metadata form. React Hook Form + Zod validation. Progress indicator.

**Acceptance Criteria:**
- File upload works, validation enforced
- Progress shown, success/error feedback

**Complexity:** L | **Dependencies:** 2, WS4-4

---

### 6. Implement Doctor Access Management
**Description:** Grant/revoke doctor access UI. Search doctors, select reports, set expiry. View active grants.

**Acceptance Criteria:**
- Patient can grant/revoke access
- Doctor list searchable, expiry configurable
- Active grants visible

**Complexity:** L | **Dependencies:** 2, WS4-8

---

### 7. Implement Emergency Contact Management
**Description:** CRUD UI for emergency contacts. Max 3 contacts.

**Acceptance Criteria:**
- Add/edit/remove emergency contacts
- Validation enforced, max limit shown

**Complexity:** M | **Dependencies:** 2, WS4-9

---

### 8. Implement WCAG 2.1 AA Compliance
**Description:** Keyboard navigation, screen reader compatibility, color contrast, aria labels throughout.

**Acceptance Criteria:**
- Lighthouse accessibility score >= 90
- Keyboard-only navigation works
- Screen reader tested

**Complexity:** L | **Dependencies:** 3, 4, 5, 6, 7

---

### 9. Implement PWA Support
**Description:** Service worker, offline report viewing (cached recently viewed), install prompt.

**Acceptance Criteria:**
- App installable on mobile
- Recently viewed reports available offline

**Complexity:** M | **Dependencies:** 3, 4

---

### 10. Implement Medical Charts/Vitals Visualization
**Description:** Recharts.js for trending test parameters over time (e.g., hemoglobin trend).

**Acceptance Criteria:**
- Charts render for trackable parameters
- Date range selectable, responsive

**Complexity:** M | **Dependencies:** 4

---

### Admin Dashboard (Refine + Material-UI + TypeScript)

### 11. Scaffold Refine + Material-UI Project
**Description:** Initialize Refine with REST data provider, Material-UI, TypeScript.

**Acceptance Criteria:**
- Dashboard loads, navigation works
- Refine CRUD scaffold functional

**Complexity:** M | **Dependencies:** None

---

### 12. Implement User Management Module
**Description:** List, view, disable/enable users. Role assignment. Search/filter.

**Acceptance Criteria:**
- Admin can list/search users, change roles, disable accounts
- Cannot view medical data

**Complexity:** L | **Dependencies:** 11, WS4-3

---

### 13. Implement Audit Log Viewer
**Description:** Searchable, filterable audit log table. Virtual scrolling for large datasets.

**Acceptance Criteria:**
- Logs displayed with pagination
- Filterable by user/action/date
- Performant with large datasets

**Complexity:** L | **Dependencies:** 11, WS6-2

---

### 14. Implement System Monitoring Dashboard
**Description:** API health status, database metrics, error rates, active users.

**Acceptance Criteria:**
- Real-time metrics displayed
- Health checks visible, error rate trends shown

**Complexity:** M | **Dependencies:** 11, WS4-11

---

### 15. Implement Report Moderation Module
**Description:** Flag suspicious uploads, review queue, approve/reject reports.

**Acceptance Criteria:**
- Flagged reports shown in queue
- Admin can approve/reject with notes, status updated

**Complexity:** M | **Dependencies:** 11, WS4-5

---

## Assumptions
- Vercel for hosting both frontends (free tier at startup)
- Separate repos or monorepo with workspaces
- API is the single backend — no BFF initially
- Chakra UI provides sufficient accessible components for WCAG 2.1 AA

## Risks
- NextAuth.js + Cognito integration may require custom adapter
- PWA offline support needs careful cache management
- Admin dashboard must NEVER expose medical data — RBAC enforced at API level
- WCAG compliance is ongoing — needs automated testing in CI
