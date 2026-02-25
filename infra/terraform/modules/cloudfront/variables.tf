variable "environment" {
  description = "Environment name"
  type        = string
}

variable "project" {
  description = "Project name"
  type        = string
}

variable "s3_bucket_domain_name" {
  description = "S3 bucket regional domain name (origin)"
  type        = string
}

variable "logs_bucket_domain_name" {
  description = "S3 logs bucket domain name for access logs (empty to disable)"
  type        = string
  default     = ""
}
