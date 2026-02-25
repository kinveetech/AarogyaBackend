output "distribution_id" {
  description = "CloudFront distribution ID"
  value       = aws_cloudfront_distribution.this.id
}

output "distribution_arn" {
  description = "CloudFront distribution ARN"
  value       = aws_cloudfront_distribution.this.arn
}

output "distribution_domain" {
  description = "CloudFront distribution domain name"
  value       = aws_cloudfront_distribution.this.domain_name
}

output "oai_iam_arn" {
  description = "CloudFront OAI IAM ARN (for S3 bucket policy)"
  value       = aws_cloudfront_origin_access_identity.this.iam_arn
}

output "key_pair_id" {
  description = "CloudFront public key ID (for signed URL generation)"
  value       = aws_cloudfront_public_key.this.id
}

output "private_key_pem" {
  description = "RSA private key PEM (for signed URL generation)"
  value       = tls_private_key.signing.private_key_pem
  sensitive   = true
}
