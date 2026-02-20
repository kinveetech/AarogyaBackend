#!/usr/bin/env bash
set -euo pipefail

if ! command -v aws >/dev/null 2>&1; then
  echo "error: aws CLI is required." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
TEMPLATE_DIR="${ROOT_DIR}/infra/aws/audit-log-archival"

AWS_REGION="${AWS_REGION:-ap-south-1}"
AWS_PROFILE_ARG=()
if [[ -n "${AWS_PROFILE:-}" ]]; then
  AWS_PROFILE_ARG=(--profile "${AWS_PROFILE}")
fi

LOG_GROUP_NAME="${LOG_GROUP_NAME:-/ecs/aarogya-api}"
S3_BUCKET_NAME="${S3_BUCKET_NAME:-aarogya-audit-archive-${AWS_REGION}}"
DELIVERY_STREAM_NAME="${DELIVERY_STREAM_NAME:-aarogya-audit-archive-stream}"
FIREHOSE_ROLE_NAME="${FIREHOSE_ROLE_NAME:-aarogya-firehose-s3-audit-role}"
CWL_TO_FIREHOSE_ROLE_NAME="${CWL_TO_FIREHOSE_ROLE_NAME:-aarogya-cwl-to-firehose-role}"

RETENTION_DAYS="${RETENTION_DAYS:-2190}"
GLACIER_TRANSITION_DAYS="${GLACIER_TRANSITION_DAYS:-30}"
S3_KMS_KEY_ARN="${S3_KMS_KEY_ARN:-}"

run_aws() {
  aws "${AWS_PROFILE_ARG[@]}" --region "${AWS_REGION}" "$@"
}

echo "Using region: ${AWS_REGION}"
echo "Using log group: ${LOG_GROUP_NAME}"
echo "Using archive bucket: ${S3_BUCKET_NAME}"
echo "Using retention days: ${RETENTION_DAYS}"
echo "Using Glacier transition days: ${GLACIER_TRANSITION_DAYS}"
if [[ -n "${S3_KMS_KEY_ARN}" ]]; then
  echo "Using KMS encryption key: ${S3_KMS_KEY_ARN}"
else
  echo "Using AES256 bucket encryption (set S3_KMS_KEY_ARN to use KMS)"
fi

AWS_ACCOUNT_ID="$(run_aws sts get-caller-identity --query Account --output text)"

create_object_lock_bucket() {
  if run_aws s3api head-bucket --bucket "${S3_BUCKET_NAME}" >/dev/null 2>&1; then
    echo "S3 bucket exists: ${S3_BUCKET_NAME}"
    return
  fi

  echo "Creating S3 bucket with Object Lock: ${S3_BUCKET_NAME}"
  if [[ "${AWS_REGION}" == "us-east-1" ]]; then
    run_aws s3api create-bucket \
      --bucket "${S3_BUCKET_NAME}" \
      --object-lock-enabled-for-bucket
  else
    run_aws s3api create-bucket \
      --bucket "${S3_BUCKET_NAME}" \
      --create-bucket-configuration "LocationConstraint=${AWS_REGION}" \
      --object-lock-enabled-for-bucket
  fi
}

configure_bucket_safety_controls() {
  echo "Enabling bucket versioning for ${S3_BUCKET_NAME}"
  run_aws s3api put-bucket-versioning \
    --bucket "${S3_BUCKET_NAME}" \
    --versioning-configuration Status=Enabled

  echo "Applying bucket public access block for ${S3_BUCKET_NAME}"
  run_aws s3api put-public-access-block \
    --bucket "${S3_BUCKET_NAME}" \
    --public-access-block-configuration \
    BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true

  if [[ -n "${S3_KMS_KEY_ARN}" ]]; then
    echo "Applying default bucket encryption with AWS KMS"
    run_aws s3api put-bucket-encryption \
      --bucket "${S3_BUCKET_NAME}" \
      --server-side-encryption-configuration \
      "Rules=[{ApplyServerSideEncryptionByDefault={SSEAlgorithm=aws:kms,KMSMasterKeyID=${S3_KMS_KEY_ARN}},BucketKeyEnabled=true}]"
  else
    echo "Applying default bucket encryption with AES256"
    run_aws s3api put-bucket-encryption \
      --bucket "${S3_BUCKET_NAME}" \
      --server-side-encryption-configuration \
      "Rules=[{ApplyServerSideEncryptionByDefault={SSEAlgorithm=AES256}}]"
  fi

  echo "Applying default Object Lock retention (COMPLIANCE mode)"
  run_aws s3api put-object-lock-configuration \
    --bucket "${S3_BUCKET_NAME}" \
    --object-lock-configuration \
    "ObjectLockEnabled=Enabled,Rule={DefaultRetention={Mode=COMPLIANCE,Days=${RETENTION_DAYS}}}"

  echo "Configuring lifecycle transition to Glacier Deep Archive"
  run_aws s3api put-bucket-lifecycle-configuration \
    --bucket "${S3_BUCKET_NAME}" \
    --lifecycle-configuration \
    "Rules=[{ID=AuditArchiveGlacierTransition,Status=Enabled,Filter={Prefix=\"\"},Transitions=[{Days=${GLACIER_TRANSITION_DAYS},StorageClass=DEEP_ARCHIVE}],NoncurrentVersionTransitions=[{NoncurrentDays=${GLACIER_TRANSITION_DAYS},StorageClass=DEEP_ARCHIVE}]}]"
}

upsert_firehose_role() {
  if run_aws iam get-role --role-name "${FIREHOSE_ROLE_NAME}" >/dev/null 2>&1; then
    echo "IAM role exists: ${FIREHOSE_ROLE_NAME}"
  else
    echo "Creating IAM role: ${FIREHOSE_ROLE_NAME}"
    run_aws iam create-role \
      --role-name "${FIREHOSE_ROLE_NAME}" \
      --assume-role-policy-document "file://${TEMPLATE_DIR}/firehose-assume-role-policy.json"
  fi

  local temp_policy_file
  temp_policy_file="$(mktemp)"
  sed "s/\${BUCKET_NAME}/${S3_BUCKET_NAME}/g" "${TEMPLATE_DIR}/firehose-access-policy.json" > "${temp_policy_file}"
  run_aws iam put-role-policy \
    --role-name "${FIREHOSE_ROLE_NAME}" \
    --policy-name AarogyaFirehoseS3AuditAccess \
    --policy-document "file://${temp_policy_file}"
  rm -f "${temp_policy_file}"
}

upsert_firehose_stream() {
  local firehose_role_arn
  firehose_role_arn="$(run_aws iam get-role --role-name "${FIREHOSE_ROLE_NAME}" --query 'Role.Arn' --output text)"

  if run_aws firehose describe-delivery-stream --delivery-stream-name "${DELIVERY_STREAM_NAME}" >/dev/null 2>&1; then
    echo "Firehose delivery stream exists: ${DELIVERY_STREAM_NAME}"
    return
  fi

  echo "Creating Firehose delivery stream: ${DELIVERY_STREAM_NAME}"
  run_aws firehose create-delivery-stream \
    --delivery-stream-name "${DELIVERY_STREAM_NAME}" \
    --delivery-stream-type DirectPut \
    --extended-s3-destination-configuration \
    "RoleARN=${firehose_role_arn},BucketARN=arn:aws:s3:::${S3_BUCKET_NAME},Prefix=cloudwatch-audit/year=!{timestamp:yyyy}/month=!{timestamp:MM}/day=!{timestamp:dd}/,ErrorOutputPrefix=cloudwatch-audit-errors/!{firehose:error-output-type}/,BufferingHints={SizeInMBs=5,IntervalInSeconds=60},CompressionFormat=GZIP"

  echo "Waiting for Firehose stream to become ACTIVE"
  run_aws firehose wait delivery-stream-active --delivery-stream-name "${DELIVERY_STREAM_NAME}"
}

upsert_cloudwatch_to_firehose_role() {
  local temp_trust_policy
  temp_trust_policy="$(mktemp)"
  sed "s/\${AWS_REGION}/${AWS_REGION}/g" "${TEMPLATE_DIR}/cloudwatch-logs-to-firehose-trust-policy.json" > "${temp_trust_policy}"

  if run_aws iam get-role --role-name "${CWL_TO_FIREHOSE_ROLE_NAME}" >/dev/null 2>&1; then
    echo "IAM role exists: ${CWL_TO_FIREHOSE_ROLE_NAME}"
  else
    echo "Creating IAM role: ${CWL_TO_FIREHOSE_ROLE_NAME}"
    run_aws iam create-role \
      --role-name "${CWL_TO_FIREHOSE_ROLE_NAME}" \
      --assume-role-policy-document "file://${temp_trust_policy}"
  fi
  rm -f "${temp_trust_policy}"

  local temp_role_policy
  temp_role_policy="$(mktemp)"
  sed \
    -e "s/\${AWS_REGION}/${AWS_REGION}/g" \
    -e "s/\${AWS_ACCOUNT_ID}/${AWS_ACCOUNT_ID}/g" \
    -e "s/\${DELIVERY_STREAM_NAME}/${DELIVERY_STREAM_NAME}/g" \
    "${TEMPLATE_DIR}/cloudwatch-logs-to-firehose-role-policy.json" > "${temp_role_policy}"

  run_aws iam put-role-policy \
    --role-name "${CWL_TO_FIREHOSE_ROLE_NAME}" \
    --policy-name AarogyaCloudWatchToFirehosePut \
    --policy-document "file://${temp_role_policy}"
  rm -f "${temp_role_policy}"
}

upsert_cloudwatch_subscription_filter() {
  local cwl_to_firehose_role_arn
  cwl_to_firehose_role_arn="$(run_aws iam get-role --role-name "${CWL_TO_FIREHOSE_ROLE_NAME}" --query 'Role.Arn' --output text)"
  local delivery_stream_arn
  delivery_stream_arn="arn:aws:firehose:${AWS_REGION}:${AWS_ACCOUNT_ID}:deliverystream/${DELIVERY_STREAM_NAME}"

  echo "Upserting CloudWatch Logs subscription filter on ${LOG_GROUP_NAME}"
  run_aws logs put-subscription-filter \
    --log-group-name "${LOG_GROUP_NAME}" \
    --filter-name AarogyaAuditArchive \
    --filter-pattern "" \
    --destination-arn "${delivery_stream_arn}" \
    --role-arn "${cwl_to_firehose_role_arn}"
}

create_object_lock_bucket
configure_bucket_safety_controls
upsert_firehose_role
upsert_firehose_stream
upsert_cloudwatch_to_firehose_role
upsert_cloudwatch_subscription_filter

echo "Audit log archival setup complete."
echo "Bucket: ${S3_BUCKET_NAME}"
echo "Delivery stream: ${DELIVERY_STREAM_NAME}"
echo "CloudWatch log group subscribed: ${LOG_GROUP_NAME}"
