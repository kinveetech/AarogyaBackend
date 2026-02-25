output "domain_identity_arn" {
  description = "SES domain identity ARN"
  value       = aws_ses_domain_identity.this.arn
}

output "dkim_tokens" {
  description = "SES DKIM tokens (DNS records auto-created in Route53)"
  value       = aws_ses_domain_dkim.this.dkim_tokens
}
