# -----------------------------------------------------------------------
# These outputs map to GitHub Actions secrets / k8s Secret values.
# After `terraform apply`, run:
#   terraform output -json > outputs.json
# to extract all values needed for deployment configuration.
# -----------------------------------------------------------------------

output "aws_access_key_id" {
  description = "IAM access key ID for the API server"
  value       = module.iam.access_key_id
  sensitive   = true
}

output "aws_secret_access_key" {
  description = "IAM secret access key for the API server"
  value       = module.iam.secret_access_key
  sensitive   = true
}

output "cognito_user_pool_id" {
  description = "Cognito user pool ID"
  value       = module.cognito.user_pool_id
}

output "cognito_app_client_id" {
  description = "Cognito app client ID"
  value       = module.cognito.app_client_id
}

output "cognito_issuer_url" {
  description = "Cognito issuer URL for JWT validation"
  value       = module.cognito.issuer_url
}

output "cognito_domain" {
  description = "Cognito hosted UI domain prefix"
  value       = module.cognito.domain
}

output "s3_bucket_name" {
  description = "S3 reports bucket name"
  value       = module.s3.bucket_id
}

output "sqs_queue_name" {
  description = "SQS queue name"
  value       = module.sqs.queue_name
}

output "kms_key_alias" {
  description = "KMS key alias"
  value       = module.kms.alias_name
}

output "ses_sender_email" {
  description = "Verified SES sender email address"
  value       = "noreply@${var.ses_domain}"
}

output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID (empty if disabled)"
  value       = var.cloudfront_enabled ? module.cloudfront[0].distribution_id : ""
}

output "cloudfront_distribution_domain" {
  description = "CloudFront domain name (empty if disabled)"
  value       = var.cloudfront_enabled ? module.cloudfront[0].distribution_domain : ""
}

output "cloudfront_key_pair_id" {
  description = "CloudFront key pair ID for signed URLs (empty if disabled)"
  value       = var.cloudfront_enabled ? module.cloudfront[0].key_pair_id : ""
}

output "cloudfront_private_key_pem" {
  description = "CloudFront signing private key PEM (empty if disabled)"
  value       = var.cloudfront_enabled ? module.cloudfront[0].private_key_pem : ""
  sensitive   = true
}
