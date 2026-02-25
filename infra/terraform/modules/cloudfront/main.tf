# --- Signing key pair for signed URLs ---

resource "tls_private_key" "signing" {
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "aws_cloudfront_public_key" "this" {
  name        = "${var.project}-${var.environment}-signing-key"
  encoded_key = tls_private_key.signing.public_key_pem
}

resource "aws_cloudfront_key_group" "this" {
  name  = "${var.project}-${var.environment}-signing-keys"
  items = [aws_cloudfront_public_key.this.id]
}

# --- Origin Access Identity ---

resource "aws_cloudfront_origin_access_identity" "this" {
  comment = "${var.project} ${var.environment} reports OAI"
}

# --- Response headers ---

resource "aws_cloudfront_response_headers_policy" "no_store" {
  name    = "${var.project}-${var.environment}-reports-no-store"
  comment = "Prevent client/proxy caching for report files"

  custom_headers_config {
    items {
      header   = "Cache-Control"
      value    = "private, no-store, max-age=0"
      override = true
    }
  }
}

# --- Distribution ---

resource "aws_cloudfront_distribution" "this" {
  enabled         = true
  is_ipv6_enabled = true
  price_class     = "PriceClass_200"
  http_version    = "http2and3"
  comment         = "${var.project} ${var.environment} report files distribution"

  dynamic "logging_config" {
    for_each = var.logs_bucket_domain_name != "" ? [1] : []
    content {
      bucket          = var.logs_bucket_domain_name
      include_cookies = false
      prefix          = "cloudfront/reports/"
    }
  }

  origin {
    domain_name = var.s3_bucket_domain_name
    origin_id   = "reports-s3-origin"

    s3_origin_config {
      origin_access_identity = aws_cloudfront_origin_access_identity.this.cloudfront_access_identity_path
    }
  }

  default_cache_behavior {
    target_origin_id       = "reports-s3-origin"
    viewer_protocol_policy = "https-only"
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    forwarded_values {
      query_string = false
      cookies {
        forward = "none"
      }
    }

    min_ttl     = 0
    default_ttl = 0
    max_ttl     = 0

    trusted_key_groups = [aws_cloudfront_key_group.this.id]

    response_headers_policy_id = aws_cloudfront_response_headers_policy.no_store.id
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }
}
