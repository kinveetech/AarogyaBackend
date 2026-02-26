variable "environment" {
  description = "Environment name"
  type        = string
}

variable "project" {
  description = "Project name"
  type        = string
}

variable "s3_bucket_arn" {
  description = "S3 reports bucket ARN"
  type        = string
}

variable "s3_quarantine_bucket_arn" {
  description = "S3 quarantine bucket ARN (virus scanning)"
  type        = string
}

variable "sqs_queue_arn" {
  description = "SQS queue ARN"
  type        = string
}

variable "kms_key_arn" {
  description = "KMS key ARN"
  type        = string
}

variable "cognito_pool_arn" {
  description = "Cognito user pool ARN"
  type        = string
}

variable "ses_domain" {
  description = "SES domain identity"
  type        = string
}

variable "cloudfront_distribution_arn" {
  description = "CloudFront distribution ARN (empty if CloudFront disabled)"
  type        = string
  default     = ""
}
