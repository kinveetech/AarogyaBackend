variable "queue_name" {
  description = "SQS queue name"
  type        = string
}

variable "s3_bucket_arn" {
  description = "S3 bucket ARN (for queue policy condition)"
  type        = string
}

variable "s3_bucket_id" {
  description = "S3 bucket ID (for notification configuration)"
  type        = string
}
