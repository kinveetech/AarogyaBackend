# Data Encryption Key Rotation

This runbook implements WS6 issue #54 requirements:

- AWS KMS automatic key rotation (annual)
- Re-encryption strategy without downtime
- Backward decryption with old keys
- Audit trail for rotation activity

## Runtime Model

1. Field-level PII encryption uses payload versioning with key metadata.
2. New writes use `Encryption:ActiveKeyId`.
3. Reads can decrypt:
   - current key payloads
   - legacy payloads via `Encryption:LegacyLocalDataKeys` (local mode)
   - historical KMS-wrapped keys (KMS mode)
4. A hosted background worker re-encrypts records in batches and logs audit events:
   - `encryption.reencryption.started`
   - `encryption.reencryption.completed`
   - `encryption.reencryption.failed`

## Configure KMS Automatic Rotation

From repo root:

```bash
AWS_REGION=ap-south-1 \
KMS_ALIAS_NAME=alias/aarogya-prod-data-key \
./scripts/configure-kms-key-rotation.sh
```

Optional:
- `AWS_PROFILE`
- `KMS_KEY_ID` (direct key id/arn)
- `CREATE_NEW_KEY=true` (if alias does not exist)

## Application Configuration

```json
{
  "Encryption": {
    "UseAwsKms": true,
    "KmsKeyId": "alias/aarogya-prod-data-key",
    "ActiveKeyId": "kms-2026",
    "LegacyLocalDataKeys": []
  },
  "EncryptionRotation": {
    "EnableBackgroundReEncryption": true,
    "CheckIntervalMinutes": 1440,
    "BatchSize": 250
  }
}
```

## Rotation Workflow (No Downtime)

1. Update `Encryption:ActiveKeyId` for the new rotation window (example: `kms-2027`).
2. Deploy application (no outage required).
3. Background service detects no completion audit for this key id.
4. Service re-encrypts PII fields in batches while API traffic continues.
5. Completion audit records touched counts for traceability.

## Local Fallback Rotation

For non-KMS local mode:

- Keep current key in `Encryption:LocalDataKey`
- Set `Encryption:ActiveKeyId` to a new id
- Move previous key material into `Encryption:LegacyLocalDataKeys`

This preserves decryption of old payloads while new writes use the active key.
