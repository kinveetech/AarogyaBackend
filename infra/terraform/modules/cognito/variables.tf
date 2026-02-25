variable "environment" {
  description = "Environment name"
  type        = string
}

variable "project" {
  description = "Project name"
  type        = string
}

variable "pool_name" {
  description = "Cognito user pool name"
  type        = string
}

variable "pre_signup_lambda_arn" {
  description = "ARN of the pre-signup Lambda trigger"
  type        = string
}

variable "google_client_id" {
  description = "Google OAuth client ID"
  type        = string
  sensitive   = true
}

variable "google_client_secret" {
  description = "Google OAuth client secret"
  type        = string
  sensitive   = true
}

variable "callback_urls" {
  description = "Allowed OAuth callback URLs"
  type        = list(string)
}

variable "mfa_configuration" {
  description = "MFA configuration (OFF, ON, OPTIONAL)"
  type        = string
  default     = "OPTIONAL"
}

variable "password_minimum_length" {
  description = "Minimum password length"
  type        = number
  default     = 8
}
