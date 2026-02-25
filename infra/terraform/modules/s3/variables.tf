variable "bucket_name" {
  description = "S3 bucket name"
  type        = string
}

variable "kms_key_arn" {
  description = "KMS key ARN for server-side encryption"
  type        = string
}
