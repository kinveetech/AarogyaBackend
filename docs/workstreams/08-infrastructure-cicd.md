# Workstream 8: Infrastructure & CI/CD

## Overview
Set up CI/CD pipelines, Docker containerization, AWS infrastructure provisioning, and production deployment.

## Tasks

### 1. Create GitHub Actions CI Pipeline
**Description:** Build, test, lint on every PR. .NET build + test, frontend build + lint.

**Acceptance Criteria:**
- PRs blocked on failing CI
- Build artifacts cached
- Test results reported

**Complexity:** M | **Dependencies:** WS1-1

---

### 2. Create Dockerfile for API
**Description:** Multi-stage Dockerfile for ASP.NET Core 9. Optimize for size (Native AOT if feasible).

**Acceptance Criteria:**
- Docker image builds and runs locally
- Image size < 200MB (or <50MB with AOT)

**Complexity:** M | **Dependencies:** WS1-1

---

### 3. Configure GitHub Actions CD Pipeline
**Description:** Deploy to AWS ECS on merge to main. Environment-specific deployments (staging, production).

**Acceptance Criteria:**
- Merge to main auto-deploys to staging
- Manual approval for production
- Rollback available

**Complexity:** L | **Dependencies:** 1, 2

---

### 4. Provision AWS Infrastructure (IaC)
**Description:** Terraform for: VPC, subnets, security groups, ALB, ECS cluster, RDS, S3, SQS, Cognito, KMS, CloudWatch.

**Acceptance Criteria:**
- Infrastructure provisioned via code
- Reproducible, state managed (S3 backend + DynamoDB lock)

**Complexity:** XL | **Dependencies:** None

---

### 5. Configure ALB with SSL
**Description:** Application Load Balancer with ACM certificate, HTTPS listener, health check routing.

**Acceptance Criteria:**
- HTTPS endpoint accessible
- HTTP redirects to HTTPS
- Health checks passing

**Complexity:** M | **Dependencies:** 4

---

### 6. Configure ECS Service
**Description:** ECS Fargate task definition, service, auto-scaling (CPU/memory based).

**Acceptance Criteria:**
- API runs on ECS Fargate
- Auto-scales based on load
- Rolling deployments work

**Complexity:** L | **Dependencies:** 2, 4

---

### 7. Configure RDS PostgreSQL
**Description:** RDS instance with encryption, automated backups, parameter group (pgcrypto enabled).

**Acceptance Criteria:**
- RDS accessible from ECS, encrypted at rest
- Automated backups configured, PITR enabled

**Complexity:** M | **Dependencies:** 4

---

### 8. Configure Route 53 DNS
**Description:** Domain routing for API (api.aarogya.in), patient portal, admin dashboard.

**Acceptance Criteria:**
- DNS records resolve correctly
- Health check routing enabled

**Complexity:** S | **Dependencies:** 4, 5

---

### 9. Configure CloudWatch Monitoring
**Description:** Dashboards for API, RDS, ECS metrics. Alarms for error rate, latency, CPU. SNS notifications.

**Acceptance Criteria:**
- Dashboard shows key metrics
- Alarms trigger on thresholds
- Notifications sent to team

**Complexity:** M | **Dependencies:** 4, 6

---

### 10. Configure WAF (Web Application Firewall)
**Description:** AWS WAF on ALB with managed rule sets (OWASP, SQL injection, XSS). Rate limiting rules.

**Acceptance Criteria:**
- WAF blocks common attacks
- Rate limits enforced
- Legitimate traffic passes

**Complexity:** M | **Dependencies:** 5

---

### 11. Set Up Staging Environment
**Description:** Separate AWS account or isolated resources for staging. Mirror production config at smaller scale.

**Acceptance Criteria:**
- Staging environment functional
- Isolated from production, data not shared

**Complexity:** L | **Dependencies:** 4

---

### 12. Configure Secrets Management
**Description:** AWS Secrets Manager for database credentials, API keys, encryption keys. ECS task role with least-privilege access.

**Acceptance Criteria:**
- No secrets in code/env vars
- All secrets from Secrets Manager
- Rotation configured

**Complexity:** M | **Dependencies:** 4

---

## Assumptions
- Terraform preferred over CloudFormation (more portable)
- ECS Fargate over EC2 for simplicity at startup (move to EKS at scale)
- Single AWS account for MVP, separate staging account later
- GitHub repository already set up

## Risks
- IaC provisioning is complex — consider manual console setup for MVP, codify later
- Terraform state management needs careful handling
- ECS Fargate costs more than EC2 per unit but saves operational overhead
- Cross-account staging adds complexity
