variable "domain" {
  description = "Domain for SES email identity"
  type        = string
}

variable "route53_zone_id" {
  description = "Route53 hosted zone ID for DNS verification records"
  type        = string
}
