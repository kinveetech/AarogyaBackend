# Audit Log Retention and Archival

This document covers the WS6 issue #53 setup for long-term audit log retention:

- CloudWatch Logs subscription to Kinesis Data Firehose
- Firehose delivery to Amazon S3 archive bucket
- S3 Object Lock in `COMPLIANCE` mode (WORM)
- Lifecycle transition to Glacier Deep Archive for cost control

## Compliance Goals

- Retain audit logs for at least 6 years (DISHA-aligned baseline)
- Enforce write-once retention controls using S3 Object Lock
- Keep storage cost-effective with Glacier-class archival

## Architecture

1. API writes structured audit events to application logs.
2. CloudWatch log group receives runtime logs from ECS/EKS/container log driver.
3. CloudWatch Logs subscription filter forwards records to Firehose.
4. Firehose batches/compresses and writes to S3.
5. S3 bucket applies Object Lock default retention and lifecycle transition to Deep Archive.

## Prerequisites

- AWS CLI v2 authenticated with permissions for:
  - `s3:*` (bucket/object lock/lifecycle on the target archive bucket)
  - `iam:*` (create/update roles + inline policies)
  - `firehose:*` (create/describe/wait delivery stream)
  - `logs:PutSubscriptionFilter`
  - `sts:GetCallerIdentity`
- A CloudWatch Logs group already exists for the API workload.

## One-Time Setup

From repository root:

```bash
AWS_REGION=ap-south-1 \
LOG_GROUP_NAME=/ecs/aarogya-api \
S3_BUCKET_NAME=aarogya-audit-archive-ap-south-1 \
./scripts/configure-audit-log-archival.sh
```

Optional environment variables:

- `AWS_PROFILE` (if using named profile)
- `DELIVERY_STREAM_NAME` (default `aarogya-audit-archive-stream`)
- `FIREHOSE_ROLE_NAME`
- `CWL_TO_FIREHOSE_ROLE_NAME`
- `RETENTION_DAYS` (default `2190` = 6 years)
- `GLACIER_TRANSITION_DAYS` (default `30`)
- `S3_KMS_KEY_ARN` (optional; if omitted, AES256 is used)

## Validation Checklist

1. Confirm bucket Object Lock:
   ```bash
   aws s3api get-object-lock-configuration --bucket <bucket>
   ```
2. Confirm retention mode and minimum period:
   - `ObjectLockEnabled=Enabled`
   - `DefaultRetention.Mode=COMPLIANCE`
   - `DefaultRetention.Days>=2190`
3. Confirm lifecycle transition:
   ```bash
   aws s3api get-bucket-lifecycle-configuration --bucket <bucket>
   ```
4. Confirm Firehose stream status:
   ```bash
   aws firehose describe-delivery-stream --delivery-stream-name <stream>
   ```
5. Confirm CloudWatch subscription filter:
   ```bash
   aws logs describe-subscription-filters --log-group-name <log-group>
   ```

## Operational Notes

- S3 Object Lock must be enabled at bucket creation time and cannot be retrofitted.
- `COMPLIANCE` mode prevents deletion/shortening of retention by root/admin users until expiry.
- Keep bucket versioning enabled at all times (required by Object Lock).
- For multi-account setups, prefer a dedicated security/compliance account for archive storage.
