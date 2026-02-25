output "access_key_id" {
  description = "IAM access key ID for the API server"
  value       = aws_iam_access_key.api.id
  sensitive   = true
}

output "secret_access_key" {
  description = "IAM secret access key for the API server"
  value       = aws_iam_access_key.api.secret
  sensitive   = true
}

output "user_arn" {
  description = "IAM user ARN"
  value       = aws_iam_user.api.arn
}
