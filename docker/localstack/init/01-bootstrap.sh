#!/usr/bin/env bash
set -euo pipefail

AWS_ENDPOINT_URL="http://localhost:4566"
AWS_REGION="${AWS_DEFAULT_REGION:-ap-south-1}"
S3_BUCKET_NAME="${LOCALSTACK_S3_BUCKET_NAME:-aarogya-dev}"
SQS_QUEUE_NAME="${LOCALSTACK_SQS_QUEUE_NAME:-aarogya-dev-queue}"
COGNITO_USER_POOL_NAME="${LOCALSTACK_COGNITO_USER_POOL_NAME:-aarogya-dev-users}"
KMS_ALIAS_NAME="${LOCALSTACK_KMS_ALIAS:-alias/aarogya-dev}"

export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-test}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-test}"
export AWS_DEFAULT_REGION="$AWS_REGION"

log() {
  echo "[localstack-init] $1"
}

log "Bootstrapping LocalStack resources in region ${AWS_REGION}"

if aws --endpoint-url "$AWS_ENDPOINT_URL" s3api head-bucket --bucket "$S3_BUCKET_NAME" >/dev/null 2>&1; then
  log "S3 bucket already exists: ${S3_BUCKET_NAME}"
else
  aws --endpoint-url "$AWS_ENDPOINT_URL" s3api create-bucket \
    --bucket "$S3_BUCKET_NAME" \
    --create-bucket-configuration "LocationConstraint=${AWS_REGION}" >/dev/null
  log "Created S3 bucket: ${S3_BUCKET_NAME}"
fi

if aws --endpoint-url "$AWS_ENDPOINT_URL" sqs get-queue-url --queue-name "$SQS_QUEUE_NAME" >/dev/null 2>&1; then
  log "SQS queue already exists: ${SQS_QUEUE_NAME}"
else
  aws --endpoint-url "$AWS_ENDPOINT_URL" sqs create-queue \
    --queue-name "$SQS_QUEUE_NAME" >/dev/null
  log "Created SQS queue: ${SQS_QUEUE_NAME}"
fi

existing_kms_alias="$(aws --endpoint-url "$AWS_ENDPOINT_URL" kms list-aliases --query "Aliases[?AliasName=='${KMS_ALIAS_NAME}'].AliasName | [0]" --output text 2>/dev/null || true)"
if [ -n "$existing_kms_alias" ] && [ "$existing_kms_alias" != "None" ]; then
  log "KMS alias already exists: ${KMS_ALIAS_NAME}"
else
  kms_key_id="$(aws --endpoint-url "$AWS_ENDPOINT_URL" kms create-key --description "Aarogya LocalStack dev key" --query 'KeyMetadata.KeyId' --output text)"
  aws --endpoint-url "$AWS_ENDPOINT_URL" kms create-alias --alias-name "$KMS_ALIAS_NAME" --target-key-id "$kms_key_id" >/dev/null
  log "Created KMS alias: ${KMS_ALIAS_NAME} (key: ${kms_key_id})"
fi

if existing_pool_id="$(aws --endpoint-url "$AWS_ENDPOINT_URL" cognito-idp list-user-pools --max-results 60 --query "UserPools[?Name=='${COGNITO_USER_POOL_NAME}'].Id | [0]" --output text 2>/tmp/localstack-cognito.err)"; then
  if [ -n "$existing_pool_id" ] && [ "$existing_pool_id" != "None" ]; then
    log "Cognito user pool already exists: ${COGNITO_USER_POOL_NAME} (${existing_pool_id})"
  else
    aws --endpoint-url "$AWS_ENDPOINT_URL" cognito-idp create-user-pool --pool-name "$COGNITO_USER_POOL_NAME" >/dev/null
    log "Created Cognito user pool: ${COGNITO_USER_POOL_NAME}"
  fi
else
  log "Cognito provisioning skipped (likely LocalStack Community limitation): $(tr '\n' ' ' </tmp/localstack-cognito.err)"
fi

log "LocalStack resource bootstrap complete"
