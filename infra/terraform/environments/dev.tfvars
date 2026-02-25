environment = "dev"
aws_region  = "ap-south-1"

# Google OAuth — pass via TF_VAR_ environment variables to avoid storing here:
#   export TF_VAR_google_client_id="..."
#   export TF_VAR_google_client_secret="..."

cognito_callback_urls = [
  "aarogya://auth/callback",
  "http://localhost:3000/api/auth/callback/cognito-pkce",
  "https://dev.kinvee.in/api/auth/callback/cognito-pkce",
]

ses_domain      = "kinvee.in"
route53_zone_id = "Z03680532RXMMGHR1HB0Y"

cloudfront_enabled = false
