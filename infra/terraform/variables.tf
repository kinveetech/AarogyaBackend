variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "ap-south-1"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
}

variable "project" {
  description = "Project name"
  type        = string
  default     = "aarogya"
}

# --- Cognito ---

variable "google_client_id" {
  description = "Google OAuth client ID for Cognito social sign-in"
  type        = string
  sensitive   = true
}

variable "google_client_secret" {
  description = "Google OAuth client secret for Cognito social sign-in"
  type        = string
  sensitive   = true
}

variable "apple_client_id" {
  description = "Apple Services ID for Cognito social sign-in"
  type        = string
  sensitive   = true
}

variable "apple_team_id" {
  description = "Apple Developer Team ID"
  type        = string
  sensitive   = true
}

variable "apple_key_id" {
  description = "Apple Sign In key ID"
  type        = string
  sensitive   = true
}

variable "apple_private_key" {
  description = "Apple Sign In private key (.p8 contents)"
  type        = string
  sensitive   = true
}

variable "facebook_app_id" {
  description = "Facebook App ID for Cognito social sign-in"
  type        = string
  sensitive   = true
}

variable "facebook_app_secret" {
  description = "Facebook App Secret for Cognito social sign-in"
  type        = string
  sensitive   = true
}

variable "cognito_callback_urls" {
  description = "Allowed callback URLs for Cognito app client"
  type        = list(string)
  default = [
    "aarogya://auth/callback",
    "http://localhost:3000/api/auth/callback/cognito-pkce",
    "https://dev.kinvee.in/api/auth/callback/cognito-pkce",
  ]
}

# --- SES ---

variable "ses_domain" {
  description = "Domain for SES email sending"
  type        = string
}

variable "route53_zone_id" {
  description = "Route53 hosted zone ID for DNS verification records"
  type        = string
}

# --- CloudFront ---

variable "cloudfront_enabled" {
  description = "Whether to create CloudFront distribution for report CDN"
  type        = bool
  default     = false
}

variable "cloudfront_logs_bucket_domain" {
  description = "S3 logs bucket domain name for CloudFront access logs (optional)"
  type        = string
  default     = ""
}
