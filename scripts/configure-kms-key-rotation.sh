#!/usr/bin/env bash
set -euo pipefail

if ! command -v aws >/dev/null 2>&1; then
  echo "error: aws CLI is required." >&2
  exit 1
fi

AWS_REGION="${AWS_REGION:-ap-south-1}"
AWS_PROFILE_ARG=()
if [[ -n "${AWS_PROFILE:-}" ]]; then
  AWS_PROFILE_ARG=(--profile "${AWS_PROFILE}")
fi

KMS_KEY_ID="${KMS_KEY_ID:-}"
KMS_ALIAS_NAME="${KMS_ALIAS_NAME:-alias/aarogya-prod-data-key}"
CREATE_NEW_KEY="${CREATE_NEW_KEY:-false}"

run_aws() {
  aws "${AWS_PROFILE_ARG[@]}" --region "${AWS_REGION}" "$@"
}

resolve_key_id() {
  if [[ -n "${KMS_KEY_ID}" ]]; then
    echo "${KMS_KEY_ID}"
    return
  fi

  if run_aws kms describe-key --key-id "${KMS_ALIAS_NAME}" >/dev/null 2>&1; then
    run_aws kms describe-key --key-id "${KMS_ALIAS_NAME}" --query 'KeyMetadata.KeyId' --output text
    return
  fi

  if [[ "${CREATE_NEW_KEY}" != "true" ]]; then
    echo "error: could not resolve key '${KMS_ALIAS_NAME}'. Set KMS_KEY_ID or CREATE_NEW_KEY=true." >&2
    exit 1
  fi

  local new_key_id
  new_key_id="$(run_aws kms create-key \
    --description "Aarogya field encryption key" \
    --query 'KeyMetadata.KeyId' \
    --output text)"

  run_aws kms create-alias --alias-name "${KMS_ALIAS_NAME}" --target-key-id "${new_key_id}"
  echo "${new_key_id}"
}

KEY_ID="$(resolve_key_id)"
echo "Using key: ${KEY_ID}"

echo "Enabling automatic key rotation..."
run_aws kms enable-key-rotation --key-id "${KEY_ID}"

STATUS="$(run_aws kms get-key-rotation-status --key-id "${KEY_ID}" --query KeyRotationEnabled --output text)"
echo "Key rotation enabled: ${STATUS}"
if [[ "${STATUS}" != "True" ]]; then
  echo "error: key rotation is not enabled." >&2
  exit 1
fi

echo "KMS key rotation setup complete."
echo "Set application configuration:"
echo "  Encryption:UseAwsKms=true"
echo "  Encryption:KmsKeyId=${KMS_ALIAS_NAME}"
echo "  Encryption:ActiveKeyId=kms-$(date +%Y)"
